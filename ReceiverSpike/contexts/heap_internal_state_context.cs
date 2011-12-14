// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

using Machine.Specifications;
using Stact;

namespace ReceiverSpike.contexts
{
	public abstract class heap_internal_state_context
		: finicky_consumer_context
	{
		protected static Future<EventHeapState> reply;

		Establish context = () =>
			{
				reply = new Future<EventHeapState>();
				AnonymousActor.New(inbox => eventFilter.Request<QueryInternals>(inbox).Receive<EventHeapState>(msg => reply.Complete(msg)));
				reply.WaitUntilCompleted(-1);
			};
	}
}