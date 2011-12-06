using System;
using System.Diagnostics.Contracts;
using C5;
using NLog;
using Stact;
using Logger = NLog.Logger;

namespace ReceiverSpike
{
	class SingleAggregateEventFilter :
		Actor
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		readonly Filter<IsSortable> _bloomFilter;
		readonly IPriorityQueue<IsSortable> _priorityQueue;
		
		/// <summary>min accepted version (ulong) of event</summary>
		ulong _maxAcceptedItem;

		/// <summary>max pending item of event</summary>
		ulong _minPendingItem;

		/// <summary>the number of duplicates this actor has seen, can be a sign of a broken msg broker</summary>
		ulong _duplicates;

		public SingleAggregateEventFilter(
			Inbox inbox, Guid aggregateId,
			Inbox parentInbox,
			int initialCapacity = 10240)
		{
			Contract.Requires(inbox != null);
			Contract.Ensures(_bloomFilter != null);

			// TODO: after initial capacity, we need to re-initialize a bloom filter!
			_bloomFilter = new BloomFilter<IsSortable>(initialCapacity);
			_priorityQueue = new IntervalHeap<IsSortable>(initialCapacity);

			inbox.Loop(loop =>
				{
					loop.Receive<Request<QueryInternals>>(msg =>
						{
							Contract.Requires(msg != null);

							msg.Respond(new EventHeapStateImpl(aggregateId, _maxAcceptedItem, _minPendingItem, _duplicates));

							loop.Continue();
						});

					loop.Receive<Request<InsertEvent>>(msg =>
						{
							Contract.Requires(msg != null);
							Contract.Requires(msg.Body.Event.AggregateId.Equals(aggregateId));

							var change = UpdateBookKeeping(msg.Body.Event);

							if ((change & BookKeepingChange.GotNext) > 0)
								msg.Respond(new EventAcceptedImpl(msg.Body.Event));

							if ((change & BookKeepingChange.GotFuture) > 0)
								_priorityQueue.Add(msg.Body.Event.Sortable());

							if ((change & BookKeepingChange.GapClosedWithMissing) > 0)
								FlushBuffer(msg);

							loop.Continue();
						});
				});
		}

		void FlushBuffer(Request<InsertEvent> msg)
		{
			while (_priorityQueue.Count > 0)
			{
				var evt = _priorityQueue.DeleteMin().Value;
				var secondChange = UpdateBookKeeping(evt);
				Contract.Assume(secondChange == BookKeepingChange.GotNext);
				msg.ResponseChannel.Send(new EventAcceptedImpl(evt));
			}
		}

		BookKeepingChange UpdateBookKeeping(Event evt)
		{
			if (evt.Version <= _maxAcceptedItem)
			{
				_logger.Trace(() => string.Format("got event with version <= mai, ignoring it. duplicates received total: {0}", 
				                                  _duplicates));

				_duplicates++;
				return BookKeepingChange.GotDuplicate;
			}

			if (evt.Version == _maxAcceptedItem + 1u)
			{
				if (_minPendingItem != _maxAcceptedItem
				    && _minPendingItem == _maxAcceptedItem + 1u)
				{
					_maxAcceptedItem++;
					return BookKeepingChange.GapClosedWithMissing | BookKeepingChange.GotNext;
				}

				_maxAcceptedItem++;
				_minPendingItem++;
				return BookKeepingChange.GotNext;
			}

			_logger.Info(() => "message reordering, got future message");

			_minPendingItem = (uint) Math.Min(_minPendingItem, evt.Version);
			return BookKeepingChange.GotFuture;
		}

		[Flags]
		enum BookKeepingChange
		{
			GotFuture = 1,
			GotDuplicate = 2,
			GotNext = 4,
			GapClosedWithMissing = 8
		}
	}
}