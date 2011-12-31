using System;
using System.Diagnostics.Contracts;

namespace ReceiverSpike.Events
{
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
}