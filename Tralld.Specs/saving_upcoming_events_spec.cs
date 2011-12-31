using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Machine.Specifications;
using TralldCLR;
using System.Linq;

namespace Specs
{	
	static class Extensions
	{
		[Pure]
		internal static string Type<T>(this T _)
			where T : Event
		{
			return typeof (T).FullName;
		}

		[Pure]
		internal static IDictionary<Guid, Internals> ToDic(this IEnumerable<Tuple<Guid,Internals>> e)
		{
			return e.ToDictionary(x => x.Item1, y => y.Item2);
		}
	}
	internal class MsgA : Event
	{
		public MsgA(Guid id, ulong version)
		{
			ArId = id;
			Version = version;
		}
		public Guid ArId { get; protected set; }
		public ulong Version { get; protected set; }
		public string Type { get { return this.Type(); } }
	}

	[Subject(typeof (MultiFilter))]
	public class initial_state_of_multi_filter_spec
	{
		static MultiFilter subject;
		static IEnumerable<Tuple<Guid, Internals>> state;
		Establish context = () => subject = new MultiFilter();
		Because of = () => state = subject.QueryState(TimeSpan.MaxValue);
		It should_have_zero_items = () => state.ShouldBeEmpty();
	}

	[Subject(typeof (MultiFilter))]
	public class two_ars_three_events_spec
	{
		static MultiFilter subject;
		static IDictionary<Guid, Internals> state;
		
		static Guid _firstAr;
		static Guid _secondAr;

		Establish context = () =>
			{
				subject = new MultiFilter();
				_firstAr = Guid.NewGuid();
				subject.Receive(new MsgA(_firstAr, 1UL));
				_secondAr = Guid.NewGuid();
				subject.Receive(new MsgA(_secondAr, 1UL));
				subject.Receive(new MsgA(_secondAr, 2UL));
			};

		Because of = () => state = subject.QueryState(TimeSpan.MaxValue).ToDic();

		static Internals Fst
		{
			get { return state[_firstAr]; }
		}

		It should_have_two_ar_ids = 
			() => state.Keys.Count.ShouldEqual(2);

		// the first ar:
		It first_ar_should_have_one_accepted_item =
			() => Fst.MaxAcceptedItem.Version.ShouldEqual(1UL);

		It first_ar_should_have_no_duplicated_items =
			() => Fst.Duplicates.ShouldEqual(0);

		It first_ar_should_have_no_pending_item =
			() => Fst.MinPendingItem.ShouldBeNull();

		It first_ar_should_have_no_futures =
			() => Fst.Futures.ShouldBeEmpty();

		static Internals Snd
		{
			get { return state[_secondAr]; }
		}

		// the second ar:
		It second_ar_should_have_second_event_as_mai =
			() => Snd.MaxAcceptedItem.Version.ShouldEqual(2UL);

		It second_ar_should_have_no_duplicates =
			() => Snd.Duplicates.ShouldEqual(0);

		It second_ar_should_have_no_min_pending_item =
			() => Snd.MinPendingItem.ShouldBeNull();

		It second_ar_should_have_no_futures =
			() => Snd.Futures.ShouldBeEmpty();
	}
}