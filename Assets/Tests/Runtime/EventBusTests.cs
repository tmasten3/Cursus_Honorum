using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;

namespace CursusHonorum.Tests.Runtime
{
    public class EventBusTests
    {
        private sealed class DummyEvent : GameEvent
        {
            public DummyEvent(string label, int year, int month, int day)
                : base(label, EventCategory.Debug, year, month, day)
            {
            }
        }

        private static GameState CreateInitializedState(out EventBus eventBus)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            eventBus = state.GetSystem<EventBus>();
            Assert.IsNotNull(eventBus, "EventBus should be registered in the default profile.");
            return state;
        }

        [Test]
        public void PublishAndSubscribe_DeliversEvents()
        {
            var state = CreateInitializedState(out var eventBus);
            try
            {
                int received = 0;
                var subscription = eventBus.Subscribe<DummyEvent>(_ => received++);

                eventBus.Publish(new DummyEvent("DummyEvent", -248, 1, 1));

                Assert.AreEqual(1, received, "Subscribed handler should receive published event exactly once.");
                Assert.IsTrue(subscription.IsActive);

                subscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void EventHistory_StoresRecentEvents()
        {
            var state = CreateInitializedState(out var eventBus);
            try
            {
                eventBus.HistoryCapacity = 8;
                var initialHistory = eventBus.HistoryCount;

                var tags = new[] { "A", "B", "C" };
                foreach (var tag in tags)
                {
                    eventBus.Publish(new DummyEvent(tag, -248, 1, 1));
                }

                Assert.AreEqual(initialHistory + tags.Length, eventBus.HistoryCount);

                var snapshot = eventBus.GetHistorySnapshot();
                Assert.GreaterOrEqual(snapshot.Count, eventBus.HistoryCount);
                Assert.GreaterOrEqual(snapshot.Count, tags.Length);

                var tail = snapshot.Skip(snapshot.Count - tags.Length).Cast<IGameEvent>().ToList();
                CollectionAssert.AllItemsAreInstancesOfType(tail, typeof(DummyEvent));
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
