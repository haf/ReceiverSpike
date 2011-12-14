// ReSharper disable InconsistentNaming
using System;
using System.Diagnostics.Contracts;

namespace ReceiverSpike
{
	public interface CompleteObservable
	{
	}

	public interface InsertEvent
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

	public interface IsQueueCompleted
	{
	}

	public interface QueryInternals
	{
		Guid AggregateId { get; }
	}

	internal class QueryInternalsImpl : QueryInternals
	{
		public QueryInternalsImpl(Guid aggregateId)
		{
			AggregateId = aggregateId;
		}

		public Guid AggregateId { get; private set; }
	}

	public interface EventHeapState
	{
		ulong MaxAcceptedItem { get; }
		ulong MinPendingItem { get; }
		ulong Duplicates { get; }
	}

	internal class EventHeapStateImpl : EventHeapState
	{
		ulong _mai, _mpi, _duplicates;

		public EventHeapStateImpl(Guid arId, ulong mai, ulong mpi, ulong duplicates)
		{
			_mai = mai;
			_mpi = mpi;
			_duplicates = duplicates;
		}

		ulong EventHeapState.MaxAcceptedItem
		{
			get { return _mai; }
		}

		ulong EventHeapState.MinPendingItem
		{
			get { return _mpi; }
		}

		ulong EventHeapState.Duplicates
		{
			get { return _duplicates; }
		}
	}

	public interface EventAccepted
	{
		Event Event { get; }
	}

	internal class EventAcceptedImpl :
		EventAccepted
	{
		public EventAcceptedImpl(Event value)
		{
			Contract.Requires(value != null);
			Event = value;
		}

		public Event Event { get; private set; }
	}

	public interface EventNotedButNotConsumed
	{
		Event Value { get; }
	}

	internal class EventNotedButNotConsumedImpl :
		EventNotedButNotConsumed
	{
		public EventNotedButNotConsumedImpl(Event value)
		{
			Contract.Requires(value != null);
			Value = value;
		}

		public Event Value { get; private set; }
	}
}