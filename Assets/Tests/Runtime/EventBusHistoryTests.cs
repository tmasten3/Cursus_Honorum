using Game.Data.Characters;
using Game.Systems.EventBus;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class EventBusHistoryTests
    {
        [Test]
        public void HistoryIsTrimmedWhenCapacityExceeded()
        {
            var bus = new EventBus(historyCapacity: 3);

            for (int day = 1; day <= 5; day++)
            {
                bus.Publish(new OnNewDayEvent(1, 1, day));
                bus.Update(null);
            }

            var snapshot = bus.GetHistorySnapshot();

            Assert.That(bus.HistoryCount, Is.EqualTo(3), "History count should match configured capacity.");
            Assert.That(snapshot.Count, Is.EqualTo(3));
            Assert.That(((OnNewDayEvent)snapshot[0]).Day, Is.EqualTo(3));
            Assert.That(((OnNewDayEvent)snapshot[1]).Day, Is.EqualTo(4));
            Assert.That(((OnNewDayEvent)snapshot[2]).Day, Is.EqualTo(5));
        }

        [Test]
        public void LoweringCapacityTrimsExistingEntries()
        {
            var bus = new EventBus(historyCapacity: 5);

            for (int day = 1; day <= 5; day++)
            {
                bus.Publish(new OnNewDayEvent(1, 1, day));
                bus.Update(null);
            }

            bus.HistoryCapacity = 2;

            var snapshot = bus.GetHistorySnapshot();

            Assert.That(bus.HistoryCount, Is.EqualTo(2));
            Assert.That(((OnNewDayEvent)snapshot[0]).Day, Is.EqualTo(4));
            Assert.That(((OnNewDayEvent)snapshot[1]).Day, Is.EqualTo(5));
        }
    }
}
