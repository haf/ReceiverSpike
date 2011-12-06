using System;

namespace ReceiverSpike
{
	public interface IsSortable
		: IEquatable<IsSortable>, IComparable<IsSortable>
	{
		Event Value { get; }
	}
}