using System;

namespace ReceiverSpike.Events
{
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
}