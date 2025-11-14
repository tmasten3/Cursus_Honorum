using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Time;

namespace CursusHonorum.Tests.Runtime
{
    public class OfficeAndElectionFlowTests
    {
        [Test]
        public void ElectionsRunEveryYear_AssignOfficesOverSpan()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var electionSystem = TestGameStateHelper.RequireSystem<ElectionSystem>(state);
                var eventBus = TestGameStateHelper.RequireSystem<EventBus>(state);

                var officeAssignments = new List<OfficeAssignedEvent>();
                var assignmentSubscription = eventBus.Subscribe<OfficeAssignedEvent>(officeAssignments.Add);

                var resultCountsByYear = new Dictionary<int, int>();
                var recordedYears = new HashSet<int>();

                int safety = 0;
                while (recordedYears.Count < 3 && safety < 2000)
                {
                    timeSystem.StepDays(1);
                    safety++;

                    var (year, month, day) = timeSystem.GetCurrentDate();
                    if (month == 7 && day >= 2 && recordedYears.Add(year))
                    {
                        var results = electionSystem.GetResultsForYear(year);
                        resultCountsByYear[year] = results?.Count ?? 0;
                    }
                }

                assignmentSubscription.Dispose();

                Assert.Less(safety, 2000, "Simulation did not progress through the expected number of years in time.");
                Assert.AreEqual(3, recordedYears.Count, "Expected to observe three election cycles.");
                Assert.IsTrue(resultCountsByYear.Values.Any(count => count > 0), "At least one year should produce election results.");
                Assert.IsTrue(officeAssignments.Count > 0, "Office assignments should occur across the simulated span.");
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void CandidateDeclarationsOccurBeforeElectionDay()
        {
            var state = TestGameStateHelper.CreateInitializedState();
            try
            {
                var timeSystem = TestGameStateHelper.RequireSystem<TimeSystem>(state);
                var electionSystem = TestGameStateHelper.RequireSystem<ElectionSystem>(state);

                var startYear = timeSystem.GetCurrentDate().year;
                bool declarationsObservedBeforeResults = false;
                bool resultsObservedAfterElection = false;
                int maxDays = 0;

                while (!resultsObservedAfterElection && maxDays < 400)
                {
                    timeSystem.StepDays(1);
                    maxDays++;

                    var (year, month, day) = timeSystem.GetCurrentDate();
                    if (year != startYear)
                    {
                        // Election results should have appeared before rolling into the next year.
                        break;
                    }

                    var declarations = electionSystem.GetDeclarationsForYear(startYear);
                    var results = electionSystem.GetResultsForYear(startYear);

                    if (!declarationsObservedBeforeResults && declarations.Count > 0 && results.Count == 0)
                    {
                        declarationsObservedBeforeResults = true;
                    }

                    if (month == 7 && day >= 2)
                    {
                        resultsObservedAfterElection = results.Count > 0;
                        break;
                    }
                }

                Assert.Less(maxDays, 400, "Advancing to the election window should complete quickly.");
                Assert.IsTrue(declarationsObservedBeforeResults, "Candidate declarations should appear before election results are recorded.");
                Assert.IsTrue(resultsObservedAfterElection, "Election results should be present after election day concludes.");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
