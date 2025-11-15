using NUnit.Framework;
using Game.Core;
using Game.Systems.Politics.Offices;
using Game.Systems.CharacterSystem;
using Game.Data.Characters;
using System.Collections.Generic;

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

                var missingSeats = new List<string>();

                foreach (var definition in officeSystem.GetAllDefinitions())
                {
                    if (definition == null)
                        continue;

                    var seats = officeSystem.StateService.GetOrCreateSeatList(definition.Id, definition.Seats);
                    if (seats == null)
                        continue;

                    for (int i = 0; i < seats.Count; i++)
                    {
                        var seat = seats[i];
                        if (seat == null)
                            continue;

                        if (!seat.HolderId.HasValue)
                        {
                            missingSeats.Add($"{definition.Id} seat {seat.SeatIndex}");
                            continue;
                        }

                        var holder = characterSystem.Get(seat.HolderId.Value);
                        Assert.IsNotNull(holder, $"Seat holder for {definition.Id} seat {seat.SeatIndex} should exist.");
                        Assert.IsTrue(holder.IsAlive, $"Seat holder for {definition.Id} seat {seat.SeatIndex} should be alive.");
                        Assert.GreaterOrEqual(holder.Age, definition.MinAge, $"Seat holder for {definition.Id} seat {seat.SeatIndex} should meet minimum age.");

                        if (definition.RequiresPlebeian)
                            Assert.AreEqual(SocialClass.Plebeian, holder.Class, $"{definition.Id} seat {seat.SeatIndex} requires plebeian class.");

                        if (definition.RequiresPatrician)
                            Assert.AreEqual(SocialClass.Patrician, holder.Class, $"{definition.Id} seat {seat.SeatIndex} requires patrician class.");

                        Assert.GreaterOrEqual(seat.EndYear, seat.StartYear, $"{definition.Id} seat {seat.SeatIndex} should have a valid term range.");
                    }
                }

                Assert.IsEmpty(missingSeats, $"All offices should be filled at start. Missing: {string.Join(", ", missingSeats)}");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
