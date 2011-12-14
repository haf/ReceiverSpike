// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

using System;
using System.Linq;
using Machine.Specifications;
using ReceiverSpike.Events;
using ReceiverSpike.contexts;
using It = Machine.Specifications.It;

namespace ReceiverSpike
{
	#region Test MassTransit Consumers

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

	[Subject(typeof(MultiAggregateEventFilter), "the subscribed values returned from the event heap")]
	public class when_inserting_multiple_events_they_are_filtered_spec 
		: finicky_consumer_context
	{
		Because I_am_adding_two_nodes_of_different_types = () => with_filter(supervisor =>
			{
				supervisor.Send(new InsertEventCmd(new MsgB(arId, 1L)));
				supervisor.Send(new InsertEventCmd(new MsgA(arId, 2L)));
			});

		It should_only_contain_one_item = () => returned.Count().ShouldEqual(1);
		It should_only_contain_a_message_b = () => returned.ShouldContainOnly(new MsgB(arId, 1L));
	}

	[Subject("the event heap")]
	public class initial_status_of_event_heap_spec
		: heap_internal_state_context
	{
		Establish that_I_dont_add_any_messages = () => with_filter(_ => { });
		It should_reply_with_event_heap_state_message = () => reply.Value.ShouldNotBeNull();
		It should_have_default_max_accepted_item_mai = () => reply.Value.MaxAcceptedItem.ShouldEqual(0UL);
	}

	[Subject(typeof(MultiAggregateEventFilter))]
	public class status_of_event_heap_after_adding_similar_messages_spec
		: heap_internal_state_context
	{
		Establish I_have_three_messages_of_type_b_added = () => with_filter(into_heap =>
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


	[Subject(typeof(MultiAggregateEventFilter))]
	public class status_of_event_heap_with_gap
		: heap_internal_state_context
	{
		Establish context_where_a_gap_of_received_messages_exists  = () => with_filter(into_heap =>
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