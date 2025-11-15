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
        public void AllOfficeSeats_HaveInitialHolders()
        {
            var state = CreateInitializedState(out var officeSystem);
            try
            {
                var definitions = officeSystem.Definitions.GetAllDefinitions();
                foreach (var definition in definitions)
                {
                    var seats = officeSystem.StateService.GetOrCreateSeatList(definition.Id, definition.Seats);
                    Assert.IsNotNull(seats, $"Office '{definition.Id}' should expose its seats.");

                    for (int i = 0; i < seats.Count; i++)
                    {
                        var seat = seats[i];
                        Assert.IsNotNull(seat, $"Office '{definition.Id}' seat {i} is missing.");
                        Assert.IsTrue(seat.HolderId.HasValue,
                            $"Office '{definition.Id}' seat {i} should be seeded with an initial holder.");
                    }
                }
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
