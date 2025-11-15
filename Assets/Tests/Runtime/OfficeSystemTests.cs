using System.Collections.Generic;
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
        public void InitialOfficeHolders_AllSeatsFilled()
        {
            var state = CreateInitializedState(out var officeSystem);
            try
            {
                var missing = new List<string>();
                foreach (var definition in officeSystem.Definitions.GetAllDefinitions())
                {
                    if (definition == null)
                        continue;

                    var seats = officeSystem.StateService.GetOrCreateSeatList(definition.Id, definition.Seats);
                    if (seats == null)
                        continue;

                    foreach (var seat in seats)
                    {
                        if (seat == null)
                            continue;

                        if (!seat.HolderId.HasValue || seat.HolderId.Value <= 0)
                            missing.Add($"{definition.Name} seat {seat.SeatIndex}");
                    }
                }

                if (missing.Count > 0)
                    Assert.Fail($"Offices missing initial holders: {string.Join(", ", missing)}");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
