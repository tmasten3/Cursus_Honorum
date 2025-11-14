using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;
using Game.Systems.BirthSystem;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.MarriageSystem;
using Game.Systems.Politics;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class InitializationTests
    {
        [Test]
        public void GameState_Initialize_WithDefaultProfile_DoesNotThrow()
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);

            Assert.DoesNotThrow(() => state.Initialize());
            Assert.IsTrue(state.IsInitialized, "GameState should report IsInitialized after Initialize().");

            state.Shutdown();
        }

        [Test]
        public void GameState_RegistersAllCoreSystems()
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();

            try
            {
                var expectedSystems = new Dictionary<Type, GameSystemBase>
                {
                    { typeof(EventBus), state.GetSystem<EventBus>() },
                    { typeof(TimeSystem), state.GetSystem<TimeSystem>() },
                    { typeof(CharacterSystem), state.GetSystem<CharacterSystem>() },
                    { typeof(BirthSystem), state.GetSystem<BirthSystem>() },
                    { typeof(MarriageSystem), state.GetSystem<MarriageSystem>() },
                    { typeof(OfficeSystem), state.GetSystem<OfficeSystem>() },
                    { typeof(ElectionSystem), state.GetSystem<ElectionSystem>() },
                    { typeof(PoliticsSystem), state.GetSystem<PoliticsSystem>() }
                };

                foreach (var entry in expectedSystems)
                {
                    Assert.IsNotNull(entry.Value, $"System '{entry.Key.Name}' should be retrievable from GameState.");
                    Assert.IsTrue(entry.Value.IsInitialized, $"System '{entry.Key.Name}' should be initialized.");
                }
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
