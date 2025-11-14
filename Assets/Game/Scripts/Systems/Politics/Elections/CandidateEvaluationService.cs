using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;
using Game.Systems.Politics.Offices;
using UnityEngine;

namespace Game.Systems.Politics.Elections
{
    public class CandidateEvaluationService
    {
        private readonly OfficeEligibilityService eligibilityService;
        private readonly System.Random rng;

        public CandidateEvaluationService(OfficeEligibilityService eligibilityService, System.Random rng)
        {
            this.eligibilityService = eligibilityService ?? throw new ArgumentNullException(nameof(eligibilityService));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public bool TryCreateDeclaration(Character character, IReadOnlyList<OfficeElectionInfo> electionInfos, int year,
            out CandidateDeclaration declaration)
        {
            declaration = null;
            if (character == null || electionInfos == null || electionInfos.Count == 0)
                return false;

            var options = new List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown, int seats)>();
            foreach (var info in electionInfos)
            {
                if (!IsEligible(character, info.Definition, year))
                    continue;

                var (score, breakdown) = EvaluateAmbition(character, info.Definition, info.SeatsAvailable);
                if (score <= 0f)
                    continue;

                options.Add((info.Definition, score, breakdown, info.SeatsAvailable));
            }

            if (options.Count == 0)
                return false;

            var prioritized = PrioritizeOfficeChoices(options);
            var choice = WeightedPick(prioritized);
            if (choice.def == null)
                return false;

            declaration = new CandidateDeclaration
            {
                CharacterId = character.ID,
                CharacterName = character.FullName,
                OfficeId = choice.def.Id,
                Office = choice.def,
                DesireScore = choice.score,
                Factors = choice.breakdown
            };

            return true;
        }

        public bool IsEligible(Character character, OfficeDefinition office, int year)
        {
            if (character == null || office == null)
                return false;

            return eligibilityService.IsEligible(character, office, year, out _);
        }

        private (OfficeDefinition def, float score, Dictionary<string, float> breakdown) WeightedPick(
            List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown, int seats)> options)
        {
            float total = options.Sum(o => Mathf.Max(0.01f, o.score));
            double roll = rng.NextDouble() * total;
            double accum = 0;
            foreach (var option in options)
            {
                accum += Mathf.Max(0.01f, option.score);
                if (roll <= accum)
                    return (option.def, option.score, option.breakdown);
            }

            var fallback = options.Last();
            return (fallback.def, fallback.score, fallback.breakdown);
        }

        private List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown, int seats)> PrioritizeOfficeChoices(
            List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown, int seats)> options)
        {
            if (options == null || options.Count == 0)
                return options;

            float maxScore = options.Max(o => o.score);
            float threshold = Mathf.Clamp(maxScore * 0.65f, 0f, maxScore);

            var viable = options.Where(o => o.score >= threshold).ToList();
            if (viable.Count == 0)
                viable = options;

            int highestRank = viable.Max(o => o.def?.Rank ?? int.MinValue);
            var prioritized = viable.Where(o => o.def != null && o.def.Rank == highestRank).ToList();

            return prioritized.Count > 0 ? prioritized : viable;
        }

        private (float score, Dictionary<string, float> breakdown) EvaluateAmbition(Character character, OfficeDefinition office,
            int seats)
        {
            var breakdown = new Dictionary<string, float>();

            float influence = character.Influence * 2.2f;
            breakdown["Influence"] = influence;

            float wealth = Mathf.Sqrt(Mathf.Max(0, character.Wealth)) * 0.6f;
            breakdown["Wealth"] = wealth;

            float ambition = HasTrait(character, "Ambitious") ? 12f : 0f;
            if (HasTrait(character, "Traditional") && office.Assembly == OfficeAssembly.ComitiaCenturiata)
                ambition += 4f;
            if (HasTrait(character, "Populist") && office.Assembly != OfficeAssembly.ComitiaCenturiata)
                ambition += 5f;
            breakdown["Traits"] = ambition;

            float classBonus = character.Class switch
            {
                SocialClass.Patrician => office.Assembly == OfficeAssembly.ComitiaCenturiata ? 10f : 6f,
                SocialClass.Plebeian => office.Assembly == OfficeAssembly.ConciliumPlebis ? 10f : 4f,
                SocialClass.Equestrian => 5f,
                _ => 0f
            };
            breakdown["Status"] = classBonus;

            float ageFactor = Mathf.Clamp01((character.Age - office.MinAge + 8f) / 25f);
            float maturity = ageFactor * 12f;
            breakdown["Maturity"] = maturity;

            float seatOpportunity = Mathf.Log10(seats + 1) * 8f;
            breakdown["Opportunity"] = seatOpportunity;

            float rankPressure = 1f + office.Rank * 0.35f;
            breakdown["Rank"] = rankPressure * 10f;

            float random = (float)(rng.NextDouble() * 6f);
            breakdown["Fortuna"] = random;

            float baseScore = influence + wealth + ambition + classBonus + maturity + seatOpportunity;
            float score = (baseScore * (0.6f + ageFactor) + random) * rankPressure;

            return (score, breakdown);
        }

        private bool HasTrait(Character character, string trait)
        {
            if (character?.Traits == null)
                return false;

            return character.Traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
        }
    }
}
