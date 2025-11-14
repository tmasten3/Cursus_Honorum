using System.IO;
using NUnit.Framework;
using Game.Core;
using Game.Systems.BirthSystem;

namespace CursusHonorum.Tests.Runtime
{
    public class BirthSystemTests
    {
        private static GameState CreateInitializedState(out BirthSystem birthSystem)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            birthSystem = state.GetSystem<BirthSystem>();
            Assert.IsNotNull(birthSystem, "BirthSystem should be available from GameState.");
            return state;
        }

        [Test]
        public void BirthSystem_UsesPopulationConfig()
        {
            var state = CreateInitializedState(out var birthSystem);
            try
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(birthSystem.ConfigPath));
                Assert.IsTrue(File.Exists(birthSystem.ConfigPath), "Population simulation config should exist at configured path.");
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void BirthSystem_SaveProducesSerializableBlob()
        {
            var state = CreateInitializedState(out var birthSystem);
            try
            {
                var data = birthSystem.Save();
                Assert.IsNotNull(data);
                Assert.IsTrue(data.ContainsKey("json"));
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
