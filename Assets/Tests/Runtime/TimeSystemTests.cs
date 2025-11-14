using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class TimeSystemTests
    {
        private static GameState CreateInitializedState(out TimeSystem timeSystem, out EventBus eventBus)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();

            timeSystem = state.GetSystem<TimeSystem>();
            eventBus = state.GetSystem<EventBus>();
            Assert.IsNotNull(timeSystem, "TimeSystem should be registered in the default profile.");
            Assert.IsNotNull(eventBus, "EventBus should be registered in the default profile.");

            return state;
        }

        [Test]
        public void Initialize_SetsStartingDate()
        {
            var state = CreateInitializedState(out var timeSystem, out _);
            try
            {
                var (year, month, day) = timeSystem.GetCurrentDate();
                Assert.AreEqual(-248, year, "Year should default to 248 BC.");
                Assert.AreEqual(1, month, "Month should begin at January.");
                Assert.AreEqual(1, day, "Day should begin at 1.");
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void StepDays_AdvancesCalendar()
        {
            var state = CreateInitializedState(out var timeSystem, out _);
            try
            {
                timeSystem.StepDays(10);
                var (year, month, day) = timeSystem.GetCurrentDate();
                Assert.AreEqual(-248, year);
                Assert.AreEqual(1, month);
                Assert.AreEqual(11, day, "Stepping ten days from January 1 should land on January 11.");
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void StepDays_RaisesOnNewDayEvents()
        {
            var state = CreateInitializedState(out var timeSystem, out var eventBus);
            try
            {
                var received = new List<OnNewDayEvent>();
                var subscription = eventBus.Subscribe<OnNewDayEvent>(e => received.Add(e));

                timeSystem.StepDays(3);

                Assert.AreEqual(3, received.Count, "Each stepped day should raise an OnNewDayEvent.");
                Assert.AreEqual(-248, received[0].Year);
                Assert.AreEqual(1, received[0].Month);
                Assert.AreEqual(2, received[0].Day);
                Assert.AreEqual(4, received[2].Day);

                subscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
