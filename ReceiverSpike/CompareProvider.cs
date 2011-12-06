using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ReceiverSpike
{
	public static class CompareProvider
	{
		static readonly IEqualityComparer<Event> _eqComp;
		static readonly IComparer<Event> _ordComp;

		static CompareProvider()
		{
			Contract.Ensures(_eqComp != null);
			Contract.Ensures(_ordComp != null);

			var comparer = new EventComparer();
			_eqComp = comparer;
			_ordComp = comparer;
		}

		[Pure]
		internal static string Type<T>(this T _)
			where T : Event
		{
			return typeof (T).FullName;
		}

		public class EventSortable : 
			IsSortable
		{
			readonly Event _event;

			public EventSortable(Event @event)
			{
				Contract.Requires(@event != null);
				_event = @event;
			}

			bool IEquatable<IsSortable>.Equals(IsSortable other)
			{
				Contract.Requires(other != null);
				return _eqComp.Equals(_event, other.Value);
			}

			int IComparable<IsSortable>.CompareTo(IsSortable other)
			{
				Contract.Requires(other != null);
				//Contract.Ensures(Contract.Result<int>() == -1 || Contract.Result<int>() == 0 || Contract.Result<int>() == 1);
				return _ordComp.Compare(_event, other.Value);
			}

			Event IsSortable.Value
			{
				get
				{
					Contract.Ensures(Contract.Result<Event>() != null);
					return _event;
				}
			}
		}

		/// <summary>
		/// Thread safe
		/// </summary>
		public class EventComparer :
			IComparer<Event>,
			IEqualityComparer<Event>
		{
			[Pure]
			bool IEqualityComparer<Event>.Equals(Event x, Event y)
			{
				Contract.Requires(x != null);
				Contract.Requires(y != null);

				return ReferenceEquals(x, y) || x.AggregateId.Equals(y.AggregateId) && x.Version.Equals(y.Version);
			}

			[Pure]
			int IEqualityComparer<Event>.GetHashCode(Event @event)
			{
				unchecked { return (@event.AggregateId.GetHashCode() * 397) ^ @event.Version.GetHashCode(); }
			}

			[Pure]
			int IComparer<Event>.Compare(Event x, Event y)
			{
				Contract.Requires(x != null);
				Contract.Requires(y != null);

				// we can skip checking the type,
				// since the invariant of events from ARs is
				// that they don't publish multiple events
				// with the same aggregate version (all events)
				// increment the AR version

				// first compare by aggregate root id
				// then by version, which is what we care about, given the above two are equal.
				return !x.AggregateId.Equals(y.AggregateId) ? x.AggregateId.CompareTo(y.AggregateId) : x.Version.CompareTo(y.Version);
			}
		}

		/// <summary>
		/// Create an immutable sortable value object from the event.
		/// </summary>
		/// <param name="event"></param>
		/// <returns></returns>
		internal static IsSortable Sortable(this Event @event)
		{
			return new EventSortable(@event);
		}
	}
}