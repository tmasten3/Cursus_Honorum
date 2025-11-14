using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Time;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics;
using Game.Systems.Politics.Offices;
using Game.Systems.Politics.Elections;
using UnityEngine;

namespace CursusHonorum.Tests.Runtime
{
    public class PoliticsSimulationTests
    {
        [Test]
        public void DeferredOfficeAssignmentsActivateOnNewYear()
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            using var harness = new SimulationHarness(includeElectionSystem: false);

            var assignedEvents = new List<OfficeAssignedEvent>();
            harness.EventBus.Subscribe<OfficeAssignedEvent>(assignedEvents.Add);

            var currentDate = harness.TimeSystem.GetCurrentDate();
            var assignYear = currentDate.year;

            var candidate = harness.CharacterSystem
                .GetAllLiving()
                .Where(c => c != null)
                .OrderBy(c => c.ID)
                .First();

            const string officeId = "quaestor";
            var descriptor = harness.OfficeSystem.AssignOffice(officeId, candidate.ID, assignYear, deferToNextYear: true);

            Assert.That(descriptor.PendingHolderId, Is.EqualTo(candidate.ID), "Pending assignment was not scheduled for the expected magistrate.");
            Assert.That(descriptor.StartYear, Is.EqualTo(assignYear + 1), "Deferred term did not target the following year.");

            harness.AdvanceDays(370);

            harness.EventBus.Update(null);
            harness.EventBus.Update(null);

            var activation = assignedEvents.FirstOrDefault(e =>
                e.OfficeId == officeId &&
                e.CharacterId == candidate.ID &&
                e.TermStartYear == assignYear + 1);

            Assert.That(activation, Is.Not.Null, "Deferred assignment never activated when the year rolled over.");
            Assert.That(activation.TermEndYear, Is.GreaterThanOrEqualTo(activation.TermStartYear), "Activated assignment carried an invalid term end year.");

            var holdings = harness.OfficeSystem.GetCurrentHoldings(candidate.ID);
            Assert.That(holdings.Any(h => h.OfficeId == officeId && h.StartYear == activation.TermStartYear),
                "Office system did not record the magistrate as holding the expected seat after activation.");
        }

        [Test]
        public void ElectionWinnerSelectionIsDeterministicWithSeededRng()
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            var firstRun = RunElectionSimulation(out var firstYear);
            var secondRun = RunElectionSimulation(out var secondYear);

            Assert.That(firstYear, Is.Not.Zero, "First simulation did not record an election year.");
            Assert.That(secondYear, Is.EqualTo(firstYear), "Election year drifted between identical simulations.");
            Assert.That(firstRun, Is.Not.Empty, "First simulation produced no election winners.");

            CollectionAssert.AreEquivalent(firstRun.Keys, secondRun.Keys, "Set of offices with election results changed between runs.");

            foreach (var officeId in firstRun.Keys)
            {
                CollectionAssert.AreEqual(firstRun[officeId], secondRun[officeId],
                    $"Winners for office '{officeId}' changed between runs despite deterministic RNG.");
            }
        }

        private static Dictionary<string, List<int>> RunElectionSimulation(out int electionYear)
        {
            using var harness = new SimulationHarness(includeElectionSystem: true);

            var completedYears = new List<int>();
            var assignmentEvents = new List<OfficeAssignedEvent>();

            harness.EventBus.Subscribe<ElectionSeasonCompletedEvent>(e => completedYears.Add(e.ElectionYear));
            harness.EventBus.Subscribe<OfficeAssignedEvent>(assignmentEvents.Add);

            harness.AdvanceDays(400);

            harness.EventBus.Update(null);
            harness.EventBus.Update(null);

            Assert.That(completedYears, Is.Not.Empty, "Election season never completed during the simulation window.");

            electionYear = completedYears[0];
            var results = harness.ElectionSystem!.GetResultsForYear(electionYear);

            Assert.That(results, Is.Not.Empty, "No election results were recorded for the completed season.");

            var winnersByOffice = results
                .ToDictionary(
                    r => r.Office.Id,
                    r => r.Winners.Select(w => w.CharacterId).OrderBy(id => id).ToList());

            var expectedAssignments = results.Sum(r => r.Winners.Count);
            var assignmentsForCycle = assignmentEvents
                .Where(e => e.TermStartYear == electionYear + 1)
                .ToList();

            Assert.That(assignmentsForCycle.Count, Is.EqualTo(expectedAssignments),
                "Number of office assignment events did not match recorded election winners.");

            return winnersByOffice;
        }

        [Test]
        public void PoliticsSystemTracksElectionCycleProgression()
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            using var harness = new SimulationHarness(includeElectionSystem: true);

            var startingCycle = harness.PoliticsSystem!.GetCurrentElectionCycle();
            Assert.That(startingCycle.Phase, Is.EqualTo(ElectionCyclePhase.QuietPeriod),
                "Politics system should begin the year in the quiet period phase.");

            harness.AdvanceDays(170);

            var openedCycle = harness.PoliticsSystem!.GetCurrentElectionCycle();
            Assert.That(openedCycle.Phase, Is.EqualTo(ElectionCyclePhase.ElectionSeasonOpen),
                "Election season did not open after advancing into June.");
            Assert.That(openedCycle.Offices, Is.Not.Empty,
                "Election cycle snapshot did not include offices when the season opened.");

            harness.AdvanceDays(40);

            var completedCycle = harness.PoliticsSystem!.GetCurrentElectionCycle();
            Assert.That(completedCycle.Phase, Is.EqualTo(ElectionCyclePhase.ResultsPublished),
                "Politics system did not transition to results after the July elections.");
            Assert.That(completedCycle.Results, Is.Not.Empty,
                "Election results were not captured by the politics system after elections concluded.");
        }

        private static string GetProjectRoot()
        {
            var testDir = TestContext.CurrentContext.TestDirectory;
            return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        }

        private sealed class SimulationHarness : IDisposable
        {
            public EventBus EventBus { get; }
            public TimeSystem TimeSystem { get; }
            public CharacterSystem CharacterSystem { get; }
            public OfficeSystem OfficeSystem { get; }
            public ElectionSystem? ElectionSystem { get; }
            public PoliticsSystem? PoliticsSystem { get; }

            private readonly string dataPath;

            public SimulationHarness(bool includeElectionSystem)
            {
                dataPath = Path.Combine(Path.GetTempPath(), "CursusHonorumTests", Path.GetRandomFileName());
                Directory.CreateDirectory(dataPath);
                Application.persistentDataPath = dataPath;

                EventBus = new EventBus();
                EventBus.Initialize(null);

                TimeSystem = new TimeSystem(EventBus);
                TimeSystem.Initialize(null);

                var simulationConfig = SimulationConfigLoader.LoadOrDefault();

                CharacterSystem = new CharacterSystem(EventBus, TimeSystem, simulationConfig);
                CharacterSystem.Initialize(null);

                OfficeSystem = new OfficeSystem(EventBus, CharacterSystem);
                OfficeSystem.Initialize(null);

                if (includeElectionSystem)
                {
                    ElectionSystem = new ElectionSystem(EventBus, TimeSystem, CharacterSystem, OfficeSystem);
                    ElectionSystem.Initialize(null);

                    PoliticsSystem = new PoliticsSystem(EventBus, TimeSystem, CharacterSystem, OfficeSystem, ElectionSystem);
                    PoliticsSystem.Initialize(null);
                }
            }

            public void AdvanceDays(int days)
            {
                if (days < 0)
                    throw new ArgumentOutOfRangeException(nameof(days), "Day count must be non-negative.");

                for (int i = 0; i < days; i++)
                {
                    TimeSystem.StepDays(1);
                    EventBus.Update(null);
                    EventBus.Update(null);
                }
            }

            public void Dispose()
            {
                PoliticsSystem?.Shutdown();
                ElectionSystem?.Shutdown();
                OfficeSystem.Shutdown();
                CharacterSystem.Shutdown();
                TimeSystem.Shutdown();
                EventBus.Shutdown();

                try
                {
                    if (Directory.Exists(dataPath))
                    {
                        Directory.Delete(dataPath, true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
