using System.Collections.Generic;
using Game.Data.Characters;
using Game.Systems.Politics.Offices;

namespace Game.Systems.Politics.Elections
{
    public class CandidateDeclaration
    {
        public int CharacterId;
        public string CharacterName;
        public string OfficeId;
        public OfficeDefinition Office;
        public float DesireScore;
        public Dictionary<string, float> Factors = new();
    }

    public class ElectionCandidate
    {
        public CandidateDeclaration Declaration;
        public Character Character;
        public Dictionary<string, float> VoteBreakdown = new();
        public float FinalScore;
    }

    public class ElectionResultRecord
    {
        public OfficeDefinition Office;
        public int Year;
        public List<ElectionCandidate> Candidates = new();
        public List<ElectionWinnerSummary> Winners = new();
    }
}
