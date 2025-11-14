using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.BirthSystem;
using Game.Systems.MarriageSystem;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class PopulationFlowTests
    {
        [Test]
        public void OverTime_PopulationChangesFromBirthsAndMarriages()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var characterSystem = TestGameStateHelper.RequireSystem<CharacterSystem>(state);
                _ = TestGameStateHelper.RequireSystem<BirthSystem>(state);
                _ = TestGameStateHelper.RequireSystem<MarriageSystem>(state);
                var eventBus = TestGameStateHelper.RequireSystem<EventBus>(state);

                var birthEvents = new List<OnCharacterBorn>();
                var marriageEvents = new List<OnCharacterMarried>();

                var birthSubscription = eventBus.Subscribe<OnCharacterBorn>(birthEvents.Add);
                var marriageSubscription = eventBus.Subscribe<OnCharacterMarried>(marriageEvents.Add);

                var initialLiving = characterSystem.GetAllLiving();
                Assert.IsNotEmpty(initialLiving, "Initial population should not be empty.");
                var initialMaxId = initialLiving.Max(c => c.ID);

                const int totalDays = 365 * 3;
                for (int i = 0; i < totalDays; i++)
                {
                    timeSystem.StepDays(1);
                }

                birthSubscription.Dispose();
                marriageSubscription.Dispose();

                var finalLiving = characterSystem.GetAllLiving();
                Assert.IsNotEmpty(finalLiving, "Population should remain non-empty after simulation.");
                var finalMaxId = finalLiving.Max(c => c.ID);

                Assert.IsTrue(birthEvents.Count > 0, "At least one birth event should occur over several years of simulation.");
                Assert.IsTrue(marriageEvents.Count > 0, "At least one marriage event should occur over several years of simulation.");
                Assert.Greater(finalMaxId, initialMaxId, "New characters should have been added to the population.");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
