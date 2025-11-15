using NUnit.Framework;
using Game.Core;
using Game.Systems.Politics.Offices;
using Game.Systems.CharacterSystem;

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
                var characterSystem = state.GetSystem<CharacterSystem>();
                Assert.IsNotNull(characterSystem, "CharacterSystem should be available for validation.");

                foreach (var definition in officeSystem.Definitions.GetAllDefinitions())
                {
                    var seats = officeSystem.StateService.GetOrCreateSeatList(definition.Id, definition.Seats);
                    Assert.IsNotNull(seats, $"Seat list for office '{definition.Id}' should be initialized.");

                    foreach (var seat in seats)
                    {
                        int holderId = seat.HolderId ?? seat.PendingHolderId ?? 0;
                        Assert.Greater(holderId, 0, $"Office '{definition.Id}' seat {seat.SeatIndex} is missing a holder.");

                        var holder = characterSystem.Get(holderId);
                        Assert.IsNotNull(holder, $"Office '{definition.Id}' seat {seat.SeatIndex} references unknown holder #{holderId}.");
                        Assert.IsTrue(holder.IsAlive, $"Office '{definition.Id}' seat {seat.SeatIndex} assigned to deceased holder #{holderId}.");
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
