// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Diagnostics.Contracts;
using System.Linq;
using Machine.Specifications;
using Moq;
using Stact;
using It = Machine.Specifications.It;
using Scheduler = System.Concurrency.Scheduler;

namespace ReceiverSpike
{
	public interface Context : IEquatable<Context>, IComparable<Context>
	{
		/// <summary>
		/// Gets the message id/request id that casued the event.
		/// </summary>
		Guid MessageId { get; }

		/// <summary>
		/// Gets the message object.
		/// </summary>
		Event Message { get; }
	}

	public interface Event
	{
		/// <summary>
		/// Gets the aggregate root id
		/// </summary>
		Guid Id { get; }

		/// <summary>
		/// Gets the event version
		/// </summary>
		ulong Version { get; }
		
		/// <summary>
		/// Gets the full type name.
		/// </summary>
		string Type { get; }
	}

	public interface IsSortable : IEquatable<IsSortable>, IComparable<IsSortable>
	{
		Event Value { get; }
	}

	interface InsertEvent
	{
		Event Event { get; }
	}


	internal class InsertEventCmd : InsertEvent
	{
		readonly Event _event;

		public InsertEventCmd(Event @event)
		{
			Contract.Requires(@event != null);
			_event = @event;
		}

		public Event Event
		{
			get { return _event; }
		}
	}

	#region Sample events

	internal class MsgA : Event
	{
		public MsgA(Guid id, ulong version)
		{
			Id = id;
			Version = version;
		}

		public Guid Id { get; protected set; }
		public ulong Version { get; protected set; }

		public string Type
		{
			get { return this.Type(); }
		}
	}

	internal class MsgB : Event
	{
		public MsgB(Guid id, ulong version)
		{
			Id = id;
			Version = version;
		}

		public Guid Id { get; protected set; }
		public ulong Version { get; protected set; }

		public string Type
		{
			get { return this.Type(); }
		}
	}

	internal class MsgC
		: Event
	{
		public MsgC(Guid id, ulong version)
		{
			Id = id;
			Version = version;
		}

		public Guid Id { get; protected set; }
		public ulong Version { get; protected set; }

		public string Type
		{
			get { return this.Type(); }
		}
	}

	#endregion

	#region future

	public interface IPersistable
	{
	}

	public static class PersistableExtensions
	{
		public static void Persist(this IPersistable persistable)
		{
		}
	}

	#endregion


	public interface QueryInternals
	{
	}

	public interface EventHeapState
	{
		ulong MaxAcceptedItem { get; }
		ulong MinPendingItem { get; }
	}

	class EventHeapStateImpl : EventHeapState
	{
		ulong _mai, _mpi;
		public EventHeapStateImpl(ulong mai, ulong mpi) { _mai = mai; _mpi = mpi; }
		ulong EventHeapState.MaxAcceptedItem { get { return _mai; } }
		ulong EventHeapState.MinPendingItem { get { return _mpi; } }
	}

	public interface EventHeapActor<T> : ActorRef, IObservable<T>
	{
		void Insert(T @event);
	}

	#region Example Consumers

	/// <summary>
	/// Interface for consumers to implement, when they want to specify what messages they want to consume.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal interface Consume<in T>
	{
		void Consume(T message);
	}

	/// <summary>
	/// Consume ONLY MsgB
	/// </summary>
	internal class FinickyConsumer
		: Consume<MsgB>
	{
		public void Consume(MsgB message)
		{
		}
	}

	/// <summary>
	/// A unfastidious consumer that listens to a great many events.
	/// </summary>
	internal class UnfastidiousConsumer
		: Consume<MsgA>, Consume<MsgB>, Consume<MsgC>
	{
		void Consume<MsgA>.Consume(MsgA message)
		{
		}

		void Consume<MsgB>.Consume(MsgB message)
		{
		}

		void Consume<MsgC>.Consume(MsgC message)
		{
		}
	}

	#endregion

	/// <summary>
	/// care about MsgB
	/// </summary>
	public abstract class finicky_consumer_context
	{
		protected static TestScheduler testScheduler;
		protected static Guid arId;
		protected static ActorRef eventHeap;
		protected static EventHeap eventHeapImpl;

		protected static IEnumerable<IsSortable> returned;

		Establish context_where_we_care_about_msgb = () =>
			{
				testScheduler = new TestScheduler();
				arId = Guid.NewGuid();
				// care about MsgB
				eventHeap = ActorFactory.Create(inbox => (eventHeapImpl = new EventHeap(typeof(FinickyConsumer), inbox, testScheduler))).GetActor();
			};

		/// <summary>
		/// after calling this method, the actor is dead
		/// </summary>
		/// <param name="action"></param>
		protected static void when_inserting(Action<EventHeap> action)
		{
			var store = new ListObservable<Event>(eventHeapImpl);

			using (eventHeapImpl)
				action(eventHeapImpl);

			testScheduler.Run();

			returned = store.Select(x => x.Sortable());
		}
	}

	[Subject(typeof(EventHeap), "the subscribed values returned from the event heap")]
	public class when_inserting_multiple_events_they_are_filtered_spec 
		: finicky_consumer_context
	{
		Because I_am_adding_two_nodes_of_different_types = () => when_inserting(heap =>
			{
				heap.Insert(new MsgB(arId, 1L).Sortable());
				heap.Insert(new MsgA(arId, 1L).Sortable());
			});

		It should_only_contain_one_item = () => returned.Count().ShouldEqual(1);
		It should_only_contain_a_message_b = () => returned.ShouldContainOnly(new MsgB(arId, 1L).Sortable());
	}

	public abstract class heap_internal_state_context
		: finicky_consumer_context
	{
		protected static Future<EventHeapState> reply;

		Establish context = () => reply = new Future<EventHeapState>();

		Because I_query_internal_state = () =>
			{
				AnonymousActor.New(inbox => eventHeap.Request<QueryInternals>(inbox).Receive<EventHeapState>(msg => reply.Complete(msg)));
				reply.WaitUntilCompleted(-1);
			};
	}

	[Subject("the event heap")]
	public class initial_status_of_event_heap_spec
		: heap_internal_state_context
	{
		Establish that_I_dont_add_any_messages = () => when_inserting(_ => { });
		It should_reply_with_event_heap_state_message = () => reply.Value.ShouldNotBeNull();
		It should_have_default_max_accepted_item_mai = () => reply.Value.MaxAcceptedItem.ShouldEqual(0UL);
	}

	[Subject(typeof(EventHeap))]
	public class status_of_event_heap_after_adding_similar_messages_spec
		: heap_internal_state_context
	{
		Establish I_have_three_messages_of_type_b_added = () => when_inserting(into_heap =>
			{
				Action<Event> i = e => into_heap.Insert(e.Sortable());
				i(new MsgA(arId, 1UL));
				i(new MsgC(arId, 2UL));
				i(new MsgB(arId, 3UL)); // B
				i(new MsgB(arId, 4UL)); // B
				i(new MsgA(arId, 5UL));
				i(new MsgB(arId, 6UL)); // B
			});

		It should_only_reply_three = 
			() => returned.Count().ShouldEqual(3);

		It should_have_max_accepted_item_mai =
			() => reply.Value.MaxAcceptedItem.ShouldEqual(6UL);

		It should_have_returned_three_msg_b = 
			() => returned.ShouldContain(new MsgB(arId, 3UL), new MsgB(arId, 4UL), new MsgB(arId, 6UL));
	}


	[Subject(typeof(EventHeap))]
	public class status_of_event_heap_with_gap
		: heap_internal_state_context
	{
		Establish context_where_a_gap_of_received_messages_exists = () =>
			{
				Action<Event> i = e => eventHeapImpl.Insert(e.Sortable());
				i(new MsgA(arId, 1UL));
				// no version=2
				// no version=3
				i(new MsgB(arId, 4UL)); // B
				i(new MsgA(arId, 5UL));
				i(new MsgB(arId, 6UL)); // B
			};

		It should_have_a_max_accepted_item_mai =
			() => reply.Value.MaxAcceptedItem.ShouldEqual(1UL);

		It should_have_a_min_pending_item =
			() => reply.Value.MinPendingItem.ShouldEqual(4UL);
	}

}