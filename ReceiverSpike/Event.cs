using System;

namespace ReceiverSpike
{
	public interface Event
	{
		/// <summary>
		/// Gets the aggregate root id
		/// </summary>
		Guid AggregateId { get; }

		/// <summary>
		/// Gets the event version
		/// </summary>
		ulong Version { get; }
		
		/// <summary>
		/// Gets the full type name.
		/// </summary>
		string Type { get; }
	}
}