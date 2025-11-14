using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Core.Save;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class SaveAndLoadIntegrationTests
    {
        [Test]
        public void SaveAndLoad_RestoresTimeAndCoreSystems()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                var repository = new SaveRepository(tempDirectory, "integration_slot.json");
                var serializer = new SaveSerializer();

                (int year, int month, int day) savedDate = default;
                int livingCount = 0;
                int targetYear = 0;
                int savedResultsCount = 0;
                int savedAssignedSeats = 0;

                var state = TestGameStateHelper.CreateInitializedState();
                try
                {
                    var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                    var characterSystem = TestGameStateHelper.RequireSystem<CharacterSystem>(state);
                    var electionSystem = TestGameStateHelper.RequireSystem<ElectionSystem>(state);
                    var officeSystem = TestGameStateHelper.RequireSystem<OfficeSystem>(state);

                    var yearsWithResults = RunUntilElectionResults(timeSystem, electionSystem, 1);
                    Assert.IsNotEmpty(yearsWithResults, "Expected at least one election cycle before saving.");

                    savedDate = timeSystem.GetCurrentDate();
                    livingCount = characterSystem.GetLiveCharacterCount();
                    targetYear = yearsWithResults[0];
                    savedResultsCount = electionSystem.GetResultsForYear(targetYear).Count;
                    savedAssignedSeats = CountAssignedSeats(officeSystem);

                    var saveService = new SaveService(state, repository, serializer);
                    var saveResult = saveService.SaveGame();
                    Assert.IsTrue(saveResult.Success, $"Save should succeed but failed with '{saveResult.ErrorMessage}'.");
                }
                finally
                {
                    state.Shutdown();
                }

                var loadedState = TestGameStateHelper.CreateInitializedState();
                try
                {
                    var loadService = new SaveService(loadedState, repository, serializer);
                    var loadResult = loadService.LoadGame();
                    Assert.IsTrue(loadResult.Success, $"Load should succeed but failed with '{loadResult.ErrorMessage}'.");

                    var loadedTimeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(loadedState);
                    var loadedCharacterSystem = TestGameStateHelper.RequireSystem<CharacterSystem>(loadedState);
                    var loadedElectionSystem = TestGameStateHelper.RequireSystem<ElectionSystem>(loadedState);
                    var loadedOfficeSystem = TestGameStateHelper.RequireSystem<OfficeSystem>(loadedState);

                    var loadedDate = loadedTimeSystem.GetCurrentDate();
                    Assert.AreEqual(savedDate.year, loadedDate.year, "Loaded year should match saved year.");
                    Assert.AreEqual(savedDate.month, loadedDate.month, "Loaded month should match saved month.");
                    Assert.AreEqual(savedDate.day, loadedDate.day, "Loaded day should match saved day.");
                    Assert.AreEqual(livingCount, loadedCharacterSystem.GetLiveCharacterCount(), "Population count should persist across save/load.");
                    Assert.AreEqual(savedResultsCount, loadedElectionSystem.GetResultsForYear(targetYear).Count, "Election results should persist for the target year.");
                    Assert.AreEqual(savedAssignedSeats, CountAssignedSeats(loadedOfficeSystem), "Office assignments should persist across save/load.");
                }
                finally
                {
                    loadedState.Shutdown();
                }
            }
            finally
            {
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Test]
        public void SaveAndLoad_MultipleSlotsIndependent()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                var repository = new SaveRepository(tempDirectory, "primary.json");
                var serializer = new SaveSerializer();

                (int year, int month, int day) firstDate;
                (int year, int month, int day) secondDate;

                var state = TestGameStateHelper.CreateInitializedState();
                try
                {
                    var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                    var electionSystem = TestGameStateHelper.RequireSystem<ElectionSystem>(state);

                    RunUntilElectionResults(timeSystem, electionSystem, 1);
                    firstDate = timeSystem.GetCurrentDate();

                    var saveService = new SaveService(state, repository, serializer);
                    var firstSave = saveService.SaveGame("slot_one");
                    Assert.IsTrue(firstSave.Success, "First slot should save successfully.");

                    RunUntilElectionResults(timeSystem, electionSystem, 1);
                    secondDate = timeSystem.GetCurrentDate();
                    Assert.AreNotEqual(firstDate, secondDate, "Simulation should advance before saving the second slot.");

                    var secondSave = saveService.SaveGame("slot_two");
                    Assert.IsTrue(secondSave.Success, "Second slot should save successfully.");
                }
                finally
                {
                    state.Shutdown();
                }

                var slotOneState = TestGameStateHelper.CreateInitializedState();
                try
                {
                    var loadService = new SaveService(slotOneState, repository, serializer);
                    var loadResult = loadService.LoadGame("slot_one");
                    Assert.IsTrue(loadResult.Success, "Slot one should load successfully.");

                    var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(slotOneState);
                    var loadedDate = timeSystem.GetCurrentDate();
                    Assert.AreEqual(firstDate, loadedDate, "Slot one should restore its own saved date.");
                }
                finally
                {
                    slotOneState.Shutdown();
                }

                var slotTwoState = TestGameStateHelper.CreateInitializedState();
                try
                {
                    var loadService = new SaveService(slotTwoState, repository, serializer);
                    var loadResult = loadService.LoadGame("slot_two");
                    Assert.IsTrue(loadResult.Success, "Slot two should load successfully.");

                    var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(slotTwoState);
                    var loadedDate = timeSystem.GetCurrentDate();
                    Assert.AreEqual(secondDate, loadedDate, "Slot two should restore its own saved date.");
                }
                finally
                {
                    slotTwoState.Shutdown();
                }
            }
            finally
            {
                DeleteTempDirectory(tempDirectory);
            }
        }

        private static List<int> RunUntilElectionResults(TimeSystem timeSystem, ElectionSystem electionSystem, int requiredYears)
        {
            var years = new List<int>();
            int safety = 0;
            while (years.Count < requiredYears && safety < 3000)
            {
                timeSystem.StepDays(1);
                safety++;
                var (year, month, day) = timeSystem.GetCurrentDate();
                if (month == 7 && day >= 2)
                {
                    var results = electionSystem.GetResultsForYear(year);
                    if (results.Count > 0 && !years.Contains(year))
                        years.Add(year);
                }
            }

            Assert.Less(safety, 3000, "Advancing to required election cycles exceeded safety threshold.");
            return years;
        }

        private static int CountAssignedSeats(OfficeSystem officeSystem)
        {
            int count = 0;
            foreach (var definition in officeSystem.GetAllDefinitions())
            {
                var seats = officeSystem.StateService.GetOrCreateSeatList(definition.Id, definition.Seats);
                if (seats == null)
                    continue;

                count += seats.Count(seat => seat != null && seat.HolderId.HasValue);
            }

            return count;
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "CursusHonorumTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors in tests.
            }
        }
    }
}
