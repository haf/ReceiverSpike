using System;

namespace ReceiverSpike
{
	public interface Context : IEquatable<Context>, IComparable<Context>
	{
		/// <summary>
		/// Gets the command id/commit id that casued the event.
		/// </summary>
		Guid CausingCommandId { get; }

		/// <summary>
		/// Gets the message object.
		/// </summary>
		Event Message { get; }
	}
}