using System.Collections.Generic;
using NUnit.Framework;
using Game.Systems.EventBus;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class TimeAndEventBusIntegrationTests
    {
        [Test]
        public void StepDays_PublishesCorrectOnNewDaySequence()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var eventBus = TestGameStateHelper.RequireSystem<EventBus>(state);

                var received = new List<(int Year, int Month, int Day)>();
                var subscription = eventBus.Subscribe<OnNewDayEvent>(e =>
                    received.Add((e.Year, e.Month, e.Day)));

                timeSystem.StepDays(5);

                Assert.AreEqual(5, received.Count, "Expected an OnNewDayEvent for each stepped day.");

                var expectedDays = new[] { 2, 3, 4, 5, 6 };
                for (int i = 0; i < expectedDays.Length; i++)
                {
                    Assert.AreEqual(-248, received[i].Year, "Year should remain constant over the first week.");
                    Assert.AreEqual(1, received[i].Month, "Month should remain January during the first week.");
                    Assert.AreEqual(expectedDays[i], received[i].Day);
                }

                subscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void CrossMonthBoundary_RaisesOnNewMonthEventOnce()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var eventBus = TestGameStateHelper.RequireSystem<EventBus>(state);

                var newMonthEvents = new List<OnNewMonthEvent>();
                var subscription = eventBus.Subscribe<OnNewMonthEvent>(newMonthEvents.Add);

                timeSystem.StepDays(31);

                Assert.AreEqual(1, newMonthEvents.Count, "Only a single OnNewMonthEvent should be raised when crossing into February.");
                var evt = newMonthEvents[0];
                Assert.AreEqual(-248, evt.Year, "Year should remain the same when moving into February.");
                Assert.AreEqual(2, evt.Month, "Crossing the January boundary should result in February.");
                Assert.AreEqual(1, evt.Day, "New month events should occur on day one of the new month.");

                subscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void CrossYearBoundary_RaisesOnNewYearEventOnce()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var eventBus = TestGameStateHelper.RequireSystem<EventBus>(state);

                var newYearEvents = new List<OnNewYearEvent>();
                var subscription = eventBus.Subscribe<OnNewYearEvent>(newYearEvents.Add);

                var startYear = timeSystem.GetCurrentDate().year;
                int safety = 0;
                while (timeSystem.GetCurrentDate().year == startYear && safety < 400)
                {
                    timeSystem.StepDays(1);
                    safety++;
                }

                Assert.Less(safety, 400, "Stepping a full year should not exceed the safety limit.");
                Assert.AreEqual(1, newYearEvents.Count, "Exactly one OnNewYearEvent should be raised when the calendar rolls over.");

                var evt = newYearEvents[0];
                int expectedYear = startYear + 1;
                if (expectedYear == 0)
                {
                    expectedYear = 1;
                }

                Assert.AreEqual(expectedYear, evt.Year, "New year event should report the incremented year.");
                Assert.AreEqual(1, evt.Month, "New year should start in January.");
                Assert.AreEqual(1, evt.Day, "New year should start on day one.");

                subscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
