// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Linq;
using Machine.Specifications;
using Stact;

namespace ReceiverSpike.contexts
{
	/// <summary>
	/// care about MsgB
	/// </summary>
	public abstract class finicky_consumer_context
	{
		protected static TestScheduler testScheduler;
		protected static Guid arId;
		protected static ActorRef eventFilter;

		protected static IList<Event> returned;

		Establish context_where_we_care_about_msgb = () =>
			{
				testScheduler = new TestScheduler();
				arId = Guid.NewGuid();
				returned = new C5.LinkedList<Event>(CompareProvider.EqComp);
				drain = AnonymousActor.New(inbox =>
					{
						inbox.Loop(loop =>
							{
								inbox.Receive<EventAccepted>(msg =>
									{
										returned.Add(msg.Event);
										loop.Continue();
									});
								loop.Continue();
							});
					});

				eventFilter = ActorFactory.Create(inbox =>
						new MultiAggregateEventFilter(typeof(FinickyConsumer), inbox, testScheduler, drain))
					.GetActor();
			};

		static ActorRef drain;

		/// <summary>
		/// after calling this method, the actor is dead
		/// </summary>
		/// <param name="action"></param>
		protected static void with_filter(Action<ActorRef> action)
		{
			action(eventFilter);
			testScheduler.Run();

			drain.SendRequestWaitForResponse<IsQueueCompleted>(TimeSpan.FromDays(1));
		}
	}
}