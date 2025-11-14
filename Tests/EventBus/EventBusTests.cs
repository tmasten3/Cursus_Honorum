using Game.Core;
using Game.Systems.EventBus;
using NUnit.Framework;

namespace CursusHonorum.Tests.EventBus
{
    public class EventBusTests
    {
        [Test]
        public void HistoryRespectsConfiguredCapacity()
        {
            var eventBus = new EventBus
            {
                HistoryCapacity = 5
            };
            eventBus.Initialize(new GameState());

            for (int i = 0; i < 20; i++)
            {
                eventBus.Publish(new OnNewDayEvent(0, 1, i + 1));
            }

            eventBus.Update(null);

            Assert.That(eventBus.History.Count, Is.EqualTo(5));
            Assert.That(eventBus.History[0].Day, Is.EqualTo(16));
            Assert.That(eventBus.History[eventBus.History.Count - 1].Day, Is.EqualTo(20));
        }

        [Test]
        public void HistoryCanBeDisabled()
        {
            var eventBus = new EventBus
            {
                HistoryCapacity = 0
            };
            eventBus.Initialize(new GameState());

            for (int i = 0; i < 10; i++)
            {
                eventBus.Publish(new OnNewDayEvent(0, 1, i + 1));
            }

            eventBus.Update(null);

            Assert.That(eventBus.History, Is.Empty);

            eventBus.HistoryCapacity = 3;

            for (int i = 0; i < 5; i++)
            {
                eventBus.Publish(new OnNewDayEvent(0, 1, i + 1));
            }

            eventBus.Update(null);

            Assert.That(eventBus.History.Count, Is.EqualTo(3));
            Assert.That(eventBus.History[0].Day, Is.EqualTo(3));
        }
    }
}
