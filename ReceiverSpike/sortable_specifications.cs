// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable FieldCanBeMadeReadOnly.Local

using System;
using Machine.Specifications;

namespace ReceiverSpike
{
	// passing
	[Subject(typeof(IsSortable))]
	public class equality_spec
	{
		static Guid someArId = Guid.NewGuid();
		static Guid anotherArId = Guid.NewGuid();

		static IsSortable type_b_sortable;
		static IsSortable type_a_sortable;

		Establish context = () =>
		{
			type_a_sortable = new MsgA(anotherArId, 1UL).Sortable();
			type_b_sortable = GenerateBSortable();
		};

		static IsSortable GenerateBSortable()
		{
			return new MsgB(someArId, 1UL).Sortable();
		}

		It should_not_equal =
			() => type_b_sortable.Equals(type_a_sortable).ShouldBeFalse();

		It should_equal_when_same_ref =
			() => type_b_sortable.Equals(type_b_sortable).ShouldBeTrue();

		It should_equal_when_not_same_ref_same_data =
			() => type_b_sortable.Equals(GenerateBSortable()).ShouldBeTrue();

		It should_not_equal_when_different_version =
			() => type_b_sortable.Equals(new MsgB(someArId, 2UL).Sortable());

		It should_not_equal_when_different_ar_id =
			() => type_b_sortable.Equals(new MsgB(anotherArId, 1UL).Sortable());
	}

	// passing
	[Subject(typeof(IsSortable))]
	public class order_spec
	{
		static Guid arId;
		static IsSortable version_one_sortable;
		static IsSortable version_two_sortable;

		Establish context = () =>
		{
			arId = Guid.NewGuid();
			version_one_sortable = new MsgA(arId, 1UL).Sortable();
			version_two_sortable = new MsgA(arId, 2UL).Sortable();
		};

		It should_compare_to_greater =
			() => version_two_sortable.CompareTo(version_one_sortable).ShouldEqual(1);

		It should_compare_to_lesser =
			() => version_one_sortable.CompareTo(version_two_sortable).ShouldEqual(-1);

		It should_compare_to_equal =
			() => version_one_sortable.CompareTo(version_one_sortable).ShouldEqual(0);
	}
}