using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Systems.Politics.Offices;

namespace CursusHonorum.Tests.Runtime
{
    public class OfficeSystemTests
    {
        private static GameState CreateInitializedState(out OfficeSystem officeSystem)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            officeSystem = state.GetSystem<OfficeSystem>();
            Assert.IsNotNull(officeSystem, "OfficeSystem should be available from GameState.");
            return state;
        }

        [Test]
        public void OfficeDefinitions_LoadSuccessfully()
        {
            var state = CreateInitializedState(out var officeSystem);
            try
            {
                Assert.Greater(officeSystem.TotalOfficesCount, 0, "Office definitions file should contain offices.");
                var consul = officeSystem.GetDefinition("consul");
                Assert.IsNotNull(consul, "Consul definition should be present in data.");
                Assert.Greater(consul.Seats, 0);
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void InitialOfficeHolders_AreSeeded()
        {
            var state = CreateInitializedState(out var officeSystem);
            try
            {
                var holdings = officeSystem.GetCurrentHoldings(283);
                Assert.IsNotNull(holdings);
                Assert.IsTrue(holdings.Any(), "Seed data should assign at least one office to character #283.");
                Assert.IsTrue(holdings.Any(h => h.OfficeId == "consul"), "Character #283 should hold a consul seat at start.");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
