using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class ElectionSystemTests
    {
        private static GameState CreateInitializedState(
            out TimeSystem timeSystem,
            out EventBus eventBus,
            out ElectionSystem electionSystem,
            out OfficeSystem officeSystem)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();

            timeSystem = state.GetSystem<TimeSystem>();
            eventBus = state.GetSystem<EventBus>();
            electionSystem = state.GetSystem<ElectionSystem>();
            officeSystem = state.GetSystem<OfficeSystem>();

            Assert.IsNotNull(timeSystem);
            Assert.IsNotNull(eventBus);
            Assert.IsNotNull(electionSystem);
            Assert.IsNotNull(officeSystem);

            return state;
        }

        [Test]
        public void ElectionSeason_FiresLifecycleEvents()
        {
            var state = CreateInitializedState(out var timeSystem, out var eventBus, out var electionSystem, out _);
            try
            {
                var openedEvents = new List<ElectionSeasonOpenedEvent>();
                var completedEvents = new List<ElectionSeasonCompletedEvent>();

                var openSubscription = eventBus.Subscribe<ElectionSeasonOpenedEvent>(openedEvents.Add);
                var completeSubscription = eventBus.Subscribe<ElectionSeasonCompletedEvent>(completedEvents.Add);

                AdvanceToDate(timeSystem, 6, 1);

                Assert.IsNotEmpty(openedEvents, "Election season should open when reaching June 1.");
                var currentYear = timeSystem.GetCurrentDate().year;
                var declarations = electionSystem.GetDeclarationsForYear(currentYear);
                Assert.IsNotNull(declarations);
                Assert.IsTrue(declarations.Count > 0, "At least one candidate should declare during election season.");

                AdvanceToDate(timeSystem, 7, 1);

                Assert.IsNotEmpty(completedEvents, "Election season should complete when reaching July 1.");
                var results = electionSystem.GetResultsForYear(currentYear);
                Assert.IsTrue(results.Count > 0, "Election results should be recorded after election day.");

                openSubscription.Dispose();
                completeSubscription.Dispose();
            }
            finally
            {
                state.Shutdown();
            }
        }

        private static void AdvanceToDate(TimeSystem timeSystem, int targetMonth, int targetDay)
        {
            var current = timeSystem.GetCurrentDate();
            int safety = 0;
            while ((current.month != targetMonth || current.day != targetDay) && safety < 2000)
            {
                timeSystem.StepDays(1);
                current = timeSystem.GetCurrentDate();
                safety++;
            }

            Assert.Less(safety, 2000, "AdvanceToDate exceeded expected iteration count.");
        }
    }
}
