using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Diagnostics.Contracts;
using System.Linq;
using Stact;

namespace ReceiverSpike
{
	/// <summary>
	/// 	Actual event heap implementation
	/// </summary>
	public class EventHeap
		: IObservable<Event>, Actor, IDisposable
	{
		readonly IScheduler _scheduler;

		readonly Subject<Event> _subject;
		readonly Func<Event, bool> _filter;

		/// <summary>
		/// Create an event heap per consumer, they are not meant to keep track of multiple
		/// consumers.
		/// </summary>
		/// <param name="eventConsumer">The type of the consumer.</param>
		/// <param name="inbox">The actor inbox</param>
		/// <param name="scheduler">The scheduler on which to schedule things</param>
		public EventHeap(Type eventConsumer, Inbox inbox, IScheduler scheduler)
		{
			Contract.Requires(eventConsumer != null);

			_scheduler = scheduler;
			_subject = new Subject<Event>(scheduler);
			_filter = ReflectFilter(eventConsumer);

			inbox.Loop(loop =>
				{
					loop.Receive<Request<QueryInternals>>(msg =>
						{
							msg.Respond(new EventHeapStateImpl(0UL, 0UL));
							loop.Continue();
						});

					loop.Receive<InsertEvent>(msg =>
						{
							Insert(msg.Event.Sortable());
							loop.Continue();
						});
				});
		}

		IDisposable IObservable<Event>.Subscribe(IObserver<Event> observer)
		{
			return _subject
				.Where(_filter)
				.Subscribe(observer);
		}

		internal void Insert(IsSortable item)
		{
			Contract.Requires(item != null);

			_subject.OnNext(item.Value);
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