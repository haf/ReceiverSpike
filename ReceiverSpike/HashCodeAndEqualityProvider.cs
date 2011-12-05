using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ReceiverSpike
{
	internal static class HashCodeAndEqualityProvider
	{
		internal static bool Equals(this Event @event, Event other)
		{
			return false;
		}

		internal static int GetHashCode(this Event @event)
		{
			return 0;
		}

		[Pure]
		internal static string Type<T>(this T @event)
			where T : Event
		{
			return typeof (T).FullName;
		}

		internal class EventSortable : IsSortable, IEqualityComparer<Event>
		{
			readonly Event _event;

			public EventSortable(Event @event)
			{
				Contract.Requires(@event != null);
				_event = @event;
			}

			public bool Equals(IsSortable other)
			{
				Contract.Requires(other != null);
				
				if (ReferenceEquals(_event, other.Value)) return true;
				
				if (!string.Equals(_event.Type, other.Value.Type, StringComparison.InvariantCulture)) 
					return false;

				return _event.Id.Equals(other.Value.Id)
				       && _event.Version.Equals(other.Value.Version);
			}

			public int CompareTo(IsSortable other)
			{
				Contract.Requires(other != null);
				return _event.Version.CompareTo(other.Value.Version);
			}

			public Event Value
			{
				get { return _event; }
			}

			bool IEqualityComparer<Event>.Equals(Event x, Event y)
			{
				return new EventSortable(x).Equals(new EventSortable(y));
			}

			int IEqualityComparer<Event>.GetHashCode(Event obj)
			{
				return new EventSortable(obj).GetHashCode();
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