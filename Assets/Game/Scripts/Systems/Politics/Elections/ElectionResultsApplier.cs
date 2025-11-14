using System;
using System.Collections.Generic;
using Game.Systems.EventBus;
using Game.Systems.Politics.Offices;

namespace Game.Systems.Politics.Elections
{
    public class ElectionResultsApplier
    {
        private readonly ElectionResultService service;

        public ElectionResultsApplier(Func<string, int, int, bool, OfficeSeatDescriptor> assignOffice, EventBus eventBus)
        {
            service = new ElectionResultService(assignOffice, eventBus);
        }

        public (ElectionResultSummary summary, ElectionResultRecord record) ApplyOfficeResults(
            OfficeDefinition office, int year, List<ElectionCandidate> candidates, List<ElectionCandidate> winners,
            float totalScore, bool debugMode, Action<string> logInfo, Action<string> logWarn)
        {
            return service.ApplyOfficeResults(office, year, candidates, winners, totalScore, debugMode, logInfo, logWarn);
        }

        public void ClearCandidateState(Dictionary<string, List<CandidateDeclaration>> declarationsByOffice,
            Dictionary<int, CandidateDeclaration> declarationByCharacter)
        {
            service.ClearCandidateState(declarationsByOffice, declarationByCharacter);
        }

        public void PublishElectionResults(int year, int month, int day, IReadOnlyList<ElectionResultSummary> summaries)
        {
            service.PublishElectionResults(year, month, day, summaries);
        }
    }
}
