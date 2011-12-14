using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Diagnostics.Contracts;
using System.Linq;
using NLog;
using Stact;
using Logger = NLog.Logger;

namespace ReceiverSpike
{
	/// <summary>
	/// 	Supervisor of the per-aggregate event filters.
	/// </summary>
	public class MultiAggregateEventFilter
		: IObservable<Event>, Actor, IDisposable
	{
		readonly IScheduler _scheduler;
		readonly ActorRef _parent;

		static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		readonly Subject<Event> _subject;
		readonly Func<Event, bool> _typeFilter;

		readonly Dictionary<Guid, ActorRef> _children;

		/// <summary>
		/// Create an event heap per consumer, they are not meant to keep track of multiple
		/// consumers.
		/// </summary>
		/// <param name="eventConsumer">The type of the consumer.</param>
		/// <param name="inbox">The actor inbox</param>
		/// <param name="scheduler">The scheduler on which to schedule things</param>
		/// <param name="parent">The parent object/actor that created this object.</param>
		public MultiAggregateEventFilter(Type eventConsumer, Inbox inbox, 
			IScheduler scheduler, ActorRef parent)
		{
			Contract.Requires(eventConsumer != null);
			Contract.Requires(inbox != null);
			Contract.Requires(scheduler != null);
			Contract.Requires(parent != null);

			_scheduler = scheduler;
			_parent = parent;

			_subject = new Subject<Event>(scheduler);
			_typeFilter = ReflectFilter(eventConsumer);
			_children = new Dictionary<Guid, ActorRef>();

			inbox.Loop(loop =>
				{
					loop.Receive<Request<QueryInternals>>(msg =>
						{
							_logger.Trace(() => string.Format("querying internals for an arId#{0}", msg.Body.AggregateId));

							// forward to actor
							GetOrCreateActor(msg.Body.AggregateId, inbox)
								.Request(msg, msg.ResponseChannel);

							loop.Continue();
						});

					loop.Receive<InsertEvent>(msg =>
						{
							_logger.Trace(() => string.Format("furthering insert event#{0}", msg.Event));

							GetOrCreateActor(msg.Event.AggregateId, inbox)
								.Request(msg, inbox);

							loop.Continue();
						});

					loop.Receive<Response<EventAccepted>>(response =>
						{
							_logger.Trace(() => "got event accepted");
							if (_typeFilter(response.Body.Event))
								_parent.Send(response.Body);
							loop.Continue();
						});
				});
		}

		ActorRef GetOrCreateActor(Guid arId, Inbox parentInbox)
		{
			if (!_children.ContainsKey(arId))
			{
				_logger.Trace(() => string.Format("hasn't seen events from AR#{0} before, creating actor",
				                                  arId));

				return _children[arId] = ActorFactory.Create(inbox =>
					new SingleAggregateEventFilter(inbox, arId, parentInbox)).GetActor();;
			}

			return _children[arId];
		}

		IDisposable IObservable<Event>.Subscribe(IObserver<Event> observer)
		{
			return _subject
				.Where(_typeFilter)
				.Subscribe(observer);
		}

		static Func<Event, bool> ReflectFilter(Type eventConsumer)
		{
			var set = new HashSet<string>(
				eventConsumer
					.GetInterfaces()
					.Where(x => x.GetGenericArguments().Length > 0)
					.SelectMany(x => x.GetGenericArguments())
					.Where(arg => typeof (Event).IsAssignableFrom(arg))
					.Select(x => x.FullName)
					.Distinct());

			return evt => set.Contains(evt.Type);
		}

		public virtual void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool isManaged)
		{
			if (!isManaged) return;
			_subject.OnCompleted();
		}
	}
}