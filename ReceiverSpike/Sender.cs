// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Linq;
using Machine.Specifications;
using Stact;
using It = Machine.Specifications.It;

namespace ReceiverSpike
{
	#region Sample Domain Events

	internal class MsgA : Event
	{
		public MsgA(Guid id, ulong version)
		{
			AggregateId = id;
			Version = version;
		}

		public Guid AggregateId { get; protected set; }
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
			AggregateId = id;
			Version = version;
		}

		public Guid AggregateId { get; protected set; }
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
			AggregateId = id;
			Version = version;
		}

		public Guid AggregateId { get; protected set; }
		public ulong Version { get; protected set; }

		public string Type
		{
			get { return this.Type(); }
		}
	}

	#endregion

	#region Persisting

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

	#region Example MassTransit Consumers

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
		protected static ActorRef eventFilter;
		protected static IObservable<Event> eventObservable; 

		protected static IEnumerable<IsSortable> returned;

		Establish context_where_we_care_about_msgb = () =>
			{
				testScheduler = new TestScheduler();
				arId = Guid.NewGuid();
				// care about MsgB
				eventFilter = ActorFactory.Create(inbox => (EventFilter)(eventObservable = new EventFilter(typeof(FinickyConsumer), inbox, testScheduler))).GetActor();
			};

		/// <summary>
		/// after calling this method, the actor is dead
		/// </summary>
		/// <param name="action"></param>
		protected static void when_inserting(Action<ActorRef> action)
		{
			var store = new ListObservable<Event>(eventObservable);
			action(eventFilter);
			eventFilter.SendRequestWaitForResponse<CompleteObservable>(TimeSpan.FromSeconds(1));
			testScheduler.Run();
			returned = store.Select(x => x.Sortable());
		}
	}

	[Subject(typeof(EventFilter), "the subscribed values returned from the event heap")]
	public class when_inserting_multiple_events_they_are_filtered_spec 
		: finicky_consumer_context
	{
		Because I_am_adding_two_nodes_of_different_types = () => when_inserting(supervisor =>
			{
				supervisor.Send(new InsertEventCmd(new MsgB(arId, 1L)));
				supervisor.Send(new InsertEventCmd(new MsgA(arId, 1L)));
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
				AnonymousActor.New(inbox => eventFilter.Request<QueryInternals>(inbox).Receive<EventHeapState>(msg => reply.Complete(msg)));
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

	[Subject(typeof(EventFilter))]
	public class status_of_event_heap_after_adding_similar_messages_spec
		: heap_internal_state_context
	{
		Establish I_have_three_messages_of_type_b_added = () => when_inserting(into_heap =>
			{
				Action<Event> i = e => into_heap.Send(new InsertEventCmd(e));
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


	[Subject(typeof(EventFilter))]
	public class status_of_event_heap_with_gap
		: heap_internal_state_context
	{
		Establish context_where_a_gap_of_received_messages_exists  = () => when_inserting(into_heap =>
			{
				Action<Event> i = e => into_heap.Send(new InsertEventCmd(e));
				i(new MsgA(arId, 1UL));
				// no version=2
				// no version=3
				i(new MsgB(arId, 4UL)); // B
				i(new MsgA(arId, 5UL));
				i(new MsgB(arId, 6UL)); // B
			});

		It should_have_a_max_accepted_item_mai =
			() => reply.Value.MaxAcceptedItem.ShouldEqual(1UL);

		It should_have_a_min_pending_item =
			() => reply.Value.MinPendingItem.ShouldEqual(4UL);
	}

}