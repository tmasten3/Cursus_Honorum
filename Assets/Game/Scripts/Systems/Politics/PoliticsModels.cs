using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Core;

namespace Game.Systems.Politics
{
    public enum ElectionCyclePhase
    {
        QuietPeriod,
        ElectionSeasonOpen,
        ResultsPublished
    }

    public sealed class ElectionCycleSnapshot
    {
        public int Year { get; }
        public ElectionCyclePhase Phase { get; }
        public IReadOnlyList<ElectionOfficeSummary> Offices { get; }
        public IReadOnlyList<ElectionResultSummary> Results { get; }
        public (int Year, int Month, int Day) SeasonOpenedOn { get; }
        public (int Year, int Month, int Day) SeasonClosedOn { get; }

        public ElectionCycleSnapshot(int year, ElectionCyclePhase phase,
            IReadOnlyList<ElectionOfficeSummary> offices,
            IReadOnlyList<ElectionResultSummary> results,
            (int Year, int Month, int Day) openedOn,
            (int Year, int Month, int Day) closedOn)
        {
            Year = year;
            Phase = phase;
            Offices = offices ?? Array.Empty<ElectionOfficeSummary>();
            Results = results ?? Array.Empty<ElectionResultSummary>();
            SeasonOpenedOn = openedOn;
            SeasonClosedOn = closedOn;
        }
    }

    public sealed class PoliticsEligibilitySnapshot
    {
        public int Year { get; }
        public IReadOnlyList<string> EligibleOfficeIds { get; }

        public PoliticsEligibilitySnapshot(int year, IReadOnlyList<string> officeIds)
        {
            Year = year;
            EligibleOfficeIds = officeIds ?? Array.Empty<string>();
        }
    }

    public sealed class OfficeTermRecord
    {
        public string OfficeId { get; }
        public string OfficeName { get; }
        public int SeatIndex { get; }
        public int StartYear { get; }
        public int EndYear { get; }

        public OfficeTermRecord(string officeId, string officeName, int seatIndex, int startYear, int endYear)
        {
            OfficeId = officeId;
            OfficeName = officeName;
            SeatIndex = seatIndex;
            StartYear = startYear;
            EndYear = endYear;
        }
    }

    internal static class PoliticsModelFactory
    {
        public static ElectionCycleSnapshot CreateCycleSnapshot(ElectionCycleState state)
        {
            var offices = new ReadOnlyCollection<ElectionOfficeSummary>(
                new List<ElectionOfficeSummary>(state.Offices));
            var results = new ReadOnlyCollection<ElectionResultSummary>(
                new List<ElectionResultSummary>(state.Results));

            return new ElectionCycleSnapshot(state.Year, state.Phase,
                offices,
                results,
                state.SeasonOpenedOn, state.SeasonClosedOn);
        }

        public sealed class ElectionCycleState
        {
            public int Year { get; private set; }
            public ElectionCyclePhase Phase { get; private set; } = ElectionCyclePhase.QuietPeriod;
            public List<ElectionOfficeSummary> Offices { get; } = new();
            public List<ElectionResultSummary> Results { get; } = new();
            public (int Year, int Month, int Day) SeasonOpenedOn { get; private set; }
                = (0, 0, 0);
            public (int Year, int Month, int Day) SeasonClosedOn { get; private set; }
                = (0, 0, 0);

            public void Reset(int year)
            {
                Year = year;
                Phase = ElectionCyclePhase.QuietPeriod;
                Offices.Clear();
                Results.Clear();
                SeasonOpenedOn = (0, 0, 0);
                SeasonClosedOn = (0, 0, 0);
            }

            public void MarkSeasonOpened(int month, int day, IReadOnlyList<ElectionOfficeSummary> summaries)
            {
                Phase = ElectionCyclePhase.ElectionSeasonOpen;
                Offices.Clear();
                if (summaries != null)
                    Offices.AddRange(summaries);
                SeasonOpenedOn = (Year, month, day);
                Results.Clear();
            }

            public void MarkSeasonCompleted(int month, int day, IReadOnlyList<ElectionResultSummary> summaries)
            {
                Phase = ElectionCyclePhase.ResultsPublished;
                Results.Clear();
                if (summaries != null)
                    Results.AddRange(summaries);
                SeasonClosedOn = (Year, month, day);
            }
        }
    }
}
