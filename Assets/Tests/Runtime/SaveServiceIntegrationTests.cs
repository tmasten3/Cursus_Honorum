using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using NUnit.Framework;
using UnityEngine;

namespace CursusHonorum.Tests.Runtime
{
    public class SaveServiceIntegrationTests
    {
        [Test]
        public void SaveAndLoadRestoresStateAndElectionProgression()
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            string tempRoot = Path.Combine(Path.GetTempPath(), "CursusHonorumTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);

            float originalDelta = Time.deltaTime;
            Time.deltaTime = 0f;

            GameState baselineState = null;
            GameState resumedState = null;

            try
            {
                Application.persistentDataPath = tempRoot;

                baselineState = new GameState();
                baselineState.Initialize();

                var baselineTime = baselineState.GetSystem<TimeSystem>();
                var baselineCharacters = baselineState.GetSystem<CharacterSystem>();
                var baselineOffices = baselineState.GetSystem<OfficeSystem>();
                var baselineElections = baselineState.GetSystem<ElectionSystem>();

                AdvanceDays(baselineState, baselineTime, 150);

                int populationBefore = baselineCharacters.CountAlive();
                var officeSnapshotBefore = CaptureHoldings(baselineCharacters, baselineOffices);
                var dateBefore = baselineTime.GetCurrentDate();
                int declarationCountBefore = baselineElections.GetDeclarationsForYear(dateBefore.year).Count;
                int resultCountBefore = baselineElections.GetResultsForYear(dateBefore.year).Count;

                var service = new SaveService(tempRoot);
                string savePath = service.Save(baselineState);

                Assert.That(savePath, Is.Not.Null.And.Not.Empty, "SaveService did not return a file path.");
                Assert.That(File.Exists(savePath), Is.True, "Save file was not written to disk.");

                resumedState = new GameState();
                resumedState.Initialize();

                var resumedTime = resumedState.GetSystem<TimeSystem>();
                var resumedCharacters = resumedState.GetSystem<CharacterSystem>();
                var resumedOffices = resumedState.GetSystem<OfficeSystem>();
                var resumedElections = resumedState.GetSystem<ElectionSystem>();

                Assert.That(service.LoadInto(resumedState), Is.True, "LoadInto should return true for an existing save.");

                Assert.That(resumedCharacters.CountAlive(), Is.EqualTo(populationBefore), "Population count mismatch after load.");
                CollectionAssert.AreEquivalent(officeSnapshotBefore, CaptureHoldings(resumedCharacters, resumedOffices));
                Assert.That(resumedTime.GetCurrentDate(), Is.EqualTo(dateBefore), "Calendar date mismatch after load.");
                Assert.That(resumedElections.GetDeclarationsForYear(dateBefore.year).Count, Is.EqualTo(declarationCountBefore));
                Assert.That(resumedElections.GetResultsForYear(dateBefore.year).Count, Is.EqualTo(resultCountBefore));

                var baselineResults = ContinueThroughElection(baselineState, baselineTime, baselineElections);
                var resumedResults = ContinueThroughElection(resumedState, resumedTime, resumedElections);

                Assert.That(resumedResults.year, Is.EqualTo(baselineResults.year));
                CollectionAssert.AreEqual(baselineResults.winners, resumedResults.winners,
                    "Election outcomes diverged after loading the save file.");
            }
            finally
            {
                Time.deltaTime = originalDelta;

                if (baselineState != null)
                    baselineState.Shutdown();
                if (resumedState != null)
                    resumedState.Shutdown();

                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, true);
                    }
                    catch
                    {
                        // ignore cleanup failures in test environment
                    }
                }
            }
        }

        private static void AdvanceDays(GameState state, TimeSystem timeSystem, int days)
        {
            for (int i = 0; i < days; i++)
            {
                timeSystem.StepDays(1);
                state.Tick(0f);
            }
        }

        private static Dictionary<string, int> CaptureHoldings(CharacterSystem characters, OfficeSystem offices)
        {
            var snapshot = new Dictionary<string, int>();
            foreach (var character in characters.GetAllLiving())
            {
                var holdings = offices.GetCurrentHoldings(character.ID);
                foreach (var holding in holdings)
                {
                    string key = $"{holding.OfficeId}:{holding.SeatIndex}";
                    snapshot[key] = holding.HolderId ?? -1;
                }
            }
            return snapshot;
        }

        private static (int year, List<string> winners) ContinueThroughElection(GameState state, TimeSystem timeSystem, ElectionSystem elections)
        {
            int year = timeSystem.GetCurrentDate().year;
            AdvanceDays(state, timeSystem, 220);

            var winners = elections.GetResultsForYear(year)
                .SelectMany(result => result.Winners.Select(winner =>
                {
                    string officeId = result.Office?.Id ?? result.Office?.Name ?? "unknown";
                    return $"{officeId}:{winner.SeatIndex}:{winner.CharacterId}";
                }))
                .OrderBy(entry => entry)
                .ToList();

            return (year, winners);
        }

        private static string GetProjectRoot()
        {
            var testDir = TestContext.CurrentContext.TestDirectory;
            return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        }
    }
}
