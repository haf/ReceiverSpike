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

		readonly TreeSet<Event> _allFutures;
		readonly IntervalHeap<Event> _futurePrioQ;
		
		/// <summary>min accepted version (ulong) of event</summary>
		ulong _maxAcceptedItem;

		/// <summary>max pending item of event</summary>
		ulong _minPendingItem = ulong.MaxValue;

		/// <summary>the number of duplicates this actor has seen, can be a sign of a broken msg broker</summary>
		ulong _duplicates;

		public SingleAggregateEventFilter(
			Inbox inbox, Guid aggregateId,
			Inbox parentInbox,
			int initialCapacity = 10240)
		{
			Contract.Requires(inbox != null);

			_allFutures = new TreeSet<Event>(CompareProvider.OrdComp, CompareProvider.EqComp);
			_futurePrioQ = new IntervalHeap<Event>(initialCapacity, CompareProvider.OrdComp);

			inbox.Loop(loop =>
				{
					loop.Receive<Request<QueryInternals>>(msg =>
						{
							Contract.Requires(msg != null);

							msg.Respond(new EventHeapStateImpl(aggregateId, _maxAcceptedItem, _minPendingItem, _duplicates));

							loop.Continue();
						});

					loop.Receive<Request<InsertEvent>>(insertReq =>
						{
							Contract.Requires(insertReq != null);
							Contract.Requires(insertReq.Body.Event.AggregateId.Equals(aggregateId));

							var @event = insertReq.Body.Event;

							var change = UpdateBookKeeping(@event);

							if ((change & BookKeepingChange.GotNext) > 0)
								insertReq.Respond(new EventAcceptedImpl(@event));

							if ((change & BookKeepingChange.GotFuture) > 0
								&& !_allFutures.Contains(@event)) // idemopotency
							{
								_allFutures.Add(@event);
								_futurePrioQ.Add(@event); // for perf
							}

							if ((change & BookKeepingChange.GapClosedWithMissing) > 0)
								ProcessFutures(insertReq);

							loop.Continue();
						});
				});
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
				    && _minPendingItem == _maxAcceptedItem + 2u)
				{
					_maxAcceptedItem++;
					return BookKeepingChange.GapClosedWithMissing | BookKeepingChange.GotNext;
				}

				_logger.Info(() => "got next message!");

				_maxAcceptedItem++;
				return BookKeepingChange.GotNext;
			}

			_logger.Info(() => "message out of order, got future message");

			_minPendingItem = (uint) Math.Min(_minPendingItem, evt.Version);
			return BookKeepingChange.GotFuture;
		}

		void ProcessFutures(Request<InsertEvent> request)
		{
			while (!_futurePrioQ.IsEmpty)
			{
				var futureEvt = _futurePrioQ.FindMin();
				Contract.Assume(futureEvt != null);
				var bookKeeping = UpdateBookKeeping(futureEvt);

				if ((bookKeeping & BookKeepingChange.GotFuture) > 0)
				{
					Contract.Assume(_minPendingItem == futureEvt.Version,
						"because the prio q was sorted and update book keeping updated to set this value");
					break;
				}

				// the removal from the tree set (_allFutures) could be done through a message
				Contract.Assume(_allFutures.Remove(_futurePrioQ.DeleteMin()));
				request.Respond(new EventAcceptedImpl(futureEvt));
			}
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