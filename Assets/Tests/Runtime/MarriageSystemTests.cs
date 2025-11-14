using System.IO;
using NUnit.Framework;
using Game.Core;
using Game.Systems.MarriageSystem;

namespace CursusHonorum.Tests.Runtime
{
    public class MarriageSystemTests
    {
        private static GameState CreateInitializedState(out MarriageSystem marriageSystem)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            marriageSystem = state.GetSystem<MarriageSystem>();
            Assert.IsNotNull(marriageSystem, "MarriageSystem should be available from GameState.");
            return state;
        }

        [Test]
        public void MarriageSystem_UsesPopulationConfig()
        {
            var state = CreateInitializedState(out var marriageSystem);
            try
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(marriageSystem.ConfigPath));
                Assert.IsTrue(File.Exists(marriageSystem.ConfigPath));
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void MarriageSystem_SaveProducesSerializableBlob()
        {
            var state = CreateInitializedState(out var marriageSystem);
            try
            {
                var data = marriageSystem.Save();
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
