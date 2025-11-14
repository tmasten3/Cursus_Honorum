using System.Collections.Generic;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Time;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class ElectionCalendarTests
    {
        [Test]
        public void Calendar_Fires_Events_On_Schedule()
        {
            var eventBus = new EventBus();
            var timeSystem = new TimeSystem(eventBus);
            var calendar = new ElectionCalendar(eventBus, timeSystem);

            var yearsStarted = new List<int>();
            var declarationDays = new List<(int Year, int Month, int Day)>();
            var electionDays = new List<(int Year, int Month, int Day)>();

            calendar.YearStarted += e => yearsStarted.Add(e.Year);
            calendar.DeclarationWindowOpened += e => declarationDays.Add((e.Year, e.Month, e.Day));
            calendar.ElectionDayArrived += e => electionDays.Add((e.Year, e.Month, e.Day));

            calendar.Initialize();

            SimulateYear(eventBus, 300);
            SimulateYear(eventBus, 301);

            Assert.That(yearsStarted, Is.EqualTo(new[] { 300, 301 }));
            Assert.That(declarationDays, Is.EqualTo(new[] { (300, 6, 1), (301, 6, 1) }));
            Assert.That(electionDays, Is.EqualTo(new[] { (300, 7, 1), (301, 7, 1) }));

            calendar.Shutdown();
        }

        private static void SimulateYear(EventBus eventBus, int year)
        {
            eventBus.Publish(new OnNewYearEvent(year, 1, 1));

            // Send a couple of ordinary days before the key milestones
            eventBus.Publish(new OnNewDayEvent(year, 1, 1));
            eventBus.Publish(new OnNewDayEvent(year, 3, 15));

            // Declaration window (June 1)
            eventBus.Publish(new OnNewDayEvent(year, 6, 1));
            eventBus.Publish(new OnNewDayEvent(year, 6, 2));

            // Election day (July 1)
            eventBus.Publish(new OnNewDayEvent(year, 7, 1));
            eventBus.Publish(new OnNewDayEvent(year, 7, 2));
        }
    }
}
