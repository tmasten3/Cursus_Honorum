using System;
using System.Collections.Generic;
using Game.Systems.EventBus;
using Game.Systems.Politics.Offices;

namespace Game.Systems.Politics.Elections
{
    public class ElectionSeasonOpenedEvent : GameEvent
    {
        public int ElectionYear { get; }
        public IReadOnlyList<ElectionOfficeSummary> Offices { get; }

        public ElectionSeasonOpenedEvent(int year, int month, int day, IReadOnlyList<ElectionOfficeSummary> offices)
            : base(nameof(ElectionSeasonOpenedEvent), year, month, day)
        {
            ElectionYear = year;
            Offices = offices ?? Array.Empty<ElectionOfficeSummary>();
        }
    }

    public class ElectionSeasonCompletedEvent : GameEvent
    {
        public int ElectionYear { get; }
        public IReadOnlyList<ElectionResultSummary> Results { get; }

        public ElectionSeasonCompletedEvent(int year, int month, int day, IReadOnlyList<ElectionResultSummary> results)
            : base(nameof(ElectionSeasonCompletedEvent), year, month, day)
        {
            ElectionYear = year;
            Results = results ?? Array.Empty<ElectionResultSummary>();
        }
    }

    public class OfficeAssignedEvent : GameEvent
    {
        public string OfficeId { get; }
        public string OfficeName { get; }
        public int CharacterId { get; }
        public string CharacterName { get; }
        public int SeatIndex { get; }
        public int TermStartYear { get; }
        public int TermEndYear { get; }

        public OfficeAssignedEvent(int year, int month, int day, string officeId, string officeName,
            int characterId, int seatIndex, int termStart, int termEnd, string characterName = null)
            : base(nameof(OfficeAssignedEvent), year, month, day)
        {
            OfficeId = officeId;
            OfficeName = officeName;
            CharacterId = characterId;
            SeatIndex = seatIndex;
            TermStartYear = termStart;
            TermEndYear = termEnd;
            CharacterName = characterName;
        }
    }

    public class ElectionOfficeSummary
    {
        public string OfficeId;
        public string OfficeName;
        public OfficeAssembly Assembly;
        public int SeatsAvailable;
    }

    public class ElectionResultSummary
    {
        public string OfficeId;
        public string OfficeName;
        public OfficeAssembly Assembly;
        public List<ElectionWinnerSummary> Winners = new();
    }

    public class ElectionWinnerSummary
    {
        public int CharacterId;
        public string CharacterName;
        public int SeatIndex;
        public float VoteScore;
        public float SupportShare;
        public string Notes;
    }
}
