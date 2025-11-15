using System.Collections.Generic;
using Game.Core;
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
        public Dictionary<string, float> Factors = new Dictionary<string, float>();
    }

    public class ElectionCandidate
    {
        public CandidateDeclaration Declaration;
        public Character Character;
        public Dictionary<string, float> VoteBreakdown = new Dictionary<string, float>();
        public float FinalScore;
    }

    public class ElectionResultRecord
    {
        public OfficeDefinition Office;
        public int Year;
        public List<ElectionCandidate> Candidates = new List<ElectionCandidate>();
        public List<ElectionWinnerSummary> Winners = new List<ElectionWinnerSummary>();
    }
}
