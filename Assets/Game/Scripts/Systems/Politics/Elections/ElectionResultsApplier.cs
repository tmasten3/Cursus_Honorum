using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Politics.Offices;
using UnityEngine;

namespace Game.Systems.Politics.Elections
{
    public class ElectionResultsApplier
    {
        private readonly OfficeSystem officeSystem;
        private readonly EventBus eventBus;

        public ElectionResultsApplier(OfficeSystem officeSystem, EventBus eventBus)
        {
            this.officeSystem = officeSystem ?? throw new ArgumentNullException(nameof(officeSystem));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public (ElectionResultSummary summary, ElectionResultRecord record) ApplyOfficeResults(
            OfficeDefinition office, int year, List<ElectionCandidate> candidates, List<ElectionCandidate> winners,
            float totalScore, bool debugMode, Action<string> logInfo, Action<string> logWarn)
        {
            var summary = new ElectionResultSummary
            {
                OfficeId = office.Id,
                OfficeName = office.Name,
                Assembly = office.Assembly,
                Winners = new List<ElectionWinnerSummary>()
            };

            var record = new ElectionResultRecord
            {
                Office = office,
                Year = year,
                Candidates = candidates,
                Winners = new List<ElectionWinnerSummary>()
            };

            var winnerEntries = new List<string>();

            foreach (var winner in winners)
            {
                var seat = officeSystem.AssignOffice(office.Id, winner.Character.ID, year);
                if (seat.SeatIndex < 0)
                {
                    logWarn?.Invoke($"{office.Name}: failed to assign seat for {winner.Character.FullName}.");
                    continue;
                }

                float share = Mathf.Max(0.1f, winner.FinalScore) / totalScore;
                string notes = ComposeWinnerNotes(winner);

                if (seat.StartYear > year)
                {
                    string startNote = $"Term begins {seat.StartYear}";
                    notes = string.IsNullOrEmpty(notes) ? startNote : $"{notes}; {startNote}";
                }

                var winnerSummary = new ElectionWinnerSummary
                {
                    CharacterId = winner.Character.ID,
                    CharacterName = winner.Character.FullName,
                    SeatIndex = seat.SeatIndex,
                    VoteScore = winner.FinalScore,
                    SupportShare = share,
                    Notes = notes
                };

                summary.Winners.Add(winnerSummary);
                record.Winners.Add(winnerSummary);
                winnerEntries.Add($"{winner.Character.FullName} (seat {seat.SeatIndex}, term {seat.StartYear}-{seat.EndYear})");

                if (debugMode)
                {
                    string detail = string.Join(", ", winner.VoteBreakdown
                        .OrderByDescending(kv => kv.Value)
                        .Take(4)
                        .Select(kv => $"{kv.Key}:{kv.Value:F1}"));
                    logInfo?.Invoke($"{winner.Character.FullName} detailed breakdown -> {detail}");
                }
            }

            string winnerSummaryLine = string.Join("; ", winnerEntries);
            logInfo?.Invoke($"{office.Name}: winners -> {winnerSummaryLine}");

            return (summary, record);
        }

        public void ClearCandidateState(Dictionary<string, List<CandidateDeclaration>> declarationsByOffice,
            Dictionary<int, CandidateDeclaration> declarationByCharacter)
        {
            declarationsByOffice?.Clear();
            declarationByCharacter?.Clear();
        }

        public void PublishElectionResults(int year, int month, int day, IReadOnlyList<ElectionResultSummary> summaries)
        {
            if (summaries == null || summaries.Count == 0)
                return;

            eventBus.Publish(new ElectionSeasonCompletedEvent(year, month, day, summaries));
        }

        private string ComposeWinnerNotes(ElectionCandidate winner)
        {
            if (winner?.VoteBreakdown == null || winner.VoteBreakdown.Count == 0)
                return string.Empty;

            var top = winner.VoteBreakdown.OrderByDescending(kv => kv.Value).Take(2).ToList();
            if (top.Count == 0)
                return string.Empty;

            if (top.Count == 1)
                return $"Backed by {top[0].Key.ToLower()}";

            return $"Backed by {top[0].Key.ToLower()} and {top[1].Key.ToLower()}";
        }
    }
}
