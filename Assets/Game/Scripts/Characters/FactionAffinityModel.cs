using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Read-only interpretation layer that classifies a character's political profile into
    /// faction affinity scores used by higher level simulations. The model combines influence
    /// leans, behavioural tendencies, and political summary labels to produce a deterministic
    /// affinity distribution across all <see cref="FactionType"/> values. The scores are
    /// normalized so they always sum to 1.0 and reflect Roman political themes:
    /// Optimates reward senatorial conservatism and honor, Populares respond to popular appeal
    /// and ambition, Moderates value balance and pragmatism, Militarists favour martial assertiveness,
    /// and Neutral captures diffuse or low-intensity personas.
    /// </summary>
    public readonly struct FactionAffinityModel
    {
        private static readonly FactionType[] Factions = (FactionType[])Enum.GetValues(typeof(FactionType));

        public IReadOnlyDictionary<FactionType, float> AffinityByFaction { get; }

        public FactionType PrimaryAffinity { get; }

        private FactionAffinityModel(Dictionary<FactionType, float> normalizedAffinities)
        {
            AffinityByFaction = new ReadOnlyDictionary<FactionType, float>(normalizedAffinities);
            PrimaryAffinity = DeterminePrimaryAffinity(normalizedAffinities);
        }

        /// <summary>
        /// Builds a faction affinity model for the supplied political descriptors. The computation is purely
        /// deterministic, has no side effects, and only depends on the provided profile, behaviour, and summary data.
        /// </summary>
        public static FactionAffinityModel FromPoliticalData(
            PoliticalProfile profile,
            PoliticalBehaviorModel behavior,
            CharacterPoliticalSummary summary)
        {
            var rawScores = ComputeRawScores(profile, behavior, summary);
            var sanitized = SanitizeScores(rawScores);
            var normalized = NormalizeScores(sanitized);
            return new FactionAffinityModel(normalized);
        }

        public float GetAffinity(FactionType faction)
        {
            if (!AffinityByFaction.TryGetValue(faction, out var value))
                return 0f;

            return value;
        }

        private static Dictionary<FactionType, float> ComputeRawScores(
            PoliticalProfile profile,
            PoliticalBehaviorModel behavior,
            CharacterPoliticalSummary summary)
        {
            var senateLean = Clamp01(profile.SenateLean);
            var popularLean = Clamp01(profile.PopularLean);
            var militaryLean = Clamp01(profile.MilitaryLean);
            var familyLean = Clamp01(profile.FamilyLean);

            var ambition = Clamp01(behavior.ShortTermOpportunism);
            var assertiveness = Clamp01(behavior.Assertiveness);
            var stability = Clamp01(behavior.Stability);
            var conservatism = Clamp01(behavior.IdeologyConservatism);
            var populism = Clamp01(behavior.IdeologyPopulism);
            var honor = Clamp01(behavior.HonorInclination);
            var opportunism = Clamp01(behavior.ShortTermOpportunism);
            var militaryAssertiveness = Clamp01(behavior.MilitaryAssertiveness);
            var longTermPlanning = Clamp01(behavior.LongTermPlanning);

            var senatePower = Clamp01(behavior.PowerBaseSenate);
            var popularPower = Clamp01(behavior.PowerBasePopular);
            var militaryPower = Clamp01(behavior.PowerBaseMilitary);

            var optimateScore =
                senateLean * 0.4f +
                conservatism * 0.2f +
                honor * 0.15f +
                stability * 0.1f +
                (1f - opportunism) * 0.05f +
                senatePower * 0.05f +
                (LabelMatches(summary.IdeologyLabel, "Optimate") ? 0.05f : 0f);

            var popularesScore =
                popularLean * 0.4f +
                populism * 0.2f +
                ambition * 0.15f +
                opportunism * 0.1f +
                (1f - honor) * 0.05f +
                popularPower * 0.05f +
                (LabelMatches(summary.IdeologyLabel, "Populares") ? 0.05f : 0f) +
                (LabelMatches(summary.PowerBaseLabel, "Popular") ? 0.05f : 0f);

            var militaristScore =
                militaryLean * 0.4f +
                militaryAssertiveness * 0.25f +
                assertiveness * 0.15f +
                Clamp01(profile.Courage / 20f) * 0.1f +
                militaryPower * 0.05f +
                (LabelMatches(summary.PowerBaseLabel, "Military") ? 0.05f : 0f);

            var senatePopularBalance = 1f - Mathf.Abs(senateLean - popularLean);
            var ideologyBalance = 1f - Mathf.Abs(conservatism - populism);
            var pragmatism = (stability + longTermPlanning) * 0.5f;
            var caution = (1f - assertiveness) * 0.5f + (1f - opportunism) * 0.5f;

            var moderateScore =
                senatePopularBalance * 0.35f +
                ideologyBalance * 0.2f +
                pragmatism * 0.25f +
                caution * 0.15f +
                familyLean * 0.05f +
                (LabelMatches(summary.PowerBaseLabel, "Diffuse") ? 0.05f : 0f);

            var strongestLean = Mathf.Max(Mathf.Max(senateLean, popularLean), Mathf.Max(militaryLean, familyLean));
            var ideologyAmbiguity = 1f - Mathf.Abs(conservatism - populism);
            var lowIntensity = 1f - strongestLean;
            var lowAmbition = 1f - ambition;
            var lowAssertiveness = 1f - assertiveness;

            var neutralScore =
                Mathf.Max(0f, lowIntensity) * 0.35f +
                Mathf.Max(0f, ideologyAmbiguity) * 0.2f +
                Mathf.Max(0f, lowAmbition) * 0.15f +
                Mathf.Max(0f, lowAssertiveness) * 0.15f +
                (LabelMatches(summary.IdeologyLabel, "Mixed") ? 0.075f : 0f) +
                (LabelMatches(summary.PowerBaseLabel, "Diffuse") ? 0.075f : 0f);

            var scores = new Dictionary<FactionType, float>
            {
                { FactionType.Optimates, optimateScore },
                { FactionType.Populares, popularesScore },
                { FactionType.Militarists, militaristScore },
                { FactionType.Moderates, moderateScore },
                { FactionType.Neutral, neutralScore }
            };

            return scores;
        }

        private static Dictionary<FactionType, float> SanitizeScores(Dictionary<FactionType, float> rawScores)
        {
            var sanitized = new Dictionary<FactionType, float>(rawScores.Count);
            foreach (var faction in Factions)
            {
                rawScores.TryGetValue(faction, out var value);
                var cleaned = Sanitize(value);
                sanitized[faction] = Mathf.Max(0f, cleaned);
            }

            return sanitized;
        }

        private static Dictionary<FactionType, float> NormalizeScores(Dictionary<FactionType, float> sanitized)
        {
            var normalized = new Dictionary<FactionType, float>(sanitized.Count);
            var total = 0f;
            foreach (var faction in Factions)
            {
                sanitized.TryGetValue(faction, out var value);
                total += value;
            }

            if (total <= 0f)
            {
                var even = 1f / sanitized.Count;
                foreach (var faction in Factions)
                {
                    normalized[faction] = even;
                }

                return normalized;
            }

            var runningTotal = 0f;
            foreach (var faction in Factions)
            {
                sanitized.TryGetValue(faction, out var value);
                var normalizedValue = value / total;
                normalizedValue = Sanitize(normalizedValue);
                normalizedValue = Mathf.Clamp01(normalizedValue);
                normalized[faction] = normalizedValue;
                runningTotal += normalizedValue;
            }

            var difference = 1f - runningTotal;
            if (Mathf.Abs(difference) > 0.0005f)
            {
                var primaryFaction = DeterminePrimaryAffinity(normalized);
                normalized[primaryFaction] = Mathf.Clamp01(normalized[primaryFaction] + difference);
            }

            var finalTotal = 0f;
            foreach (var faction in Factions)
            {
                finalTotal += normalized[faction];
            }

            if (Mathf.Abs(1f - finalTotal) > 0.0005f && finalTotal > 0f)
            {
                var correction = 1f / finalTotal;
                foreach (var faction in Factions)
                {
                    normalized[faction] = Mathf.Clamp01(normalized[faction] * correction);
                }
            }

            return normalized;
        }

        private static FactionType DeterminePrimaryAffinity(Dictionary<FactionType, float> scores)
        {
            var bestFaction = FactionType.Neutral;
            var bestValue = float.MinValue;

            foreach (var faction in Factions)
            {
                scores.TryGetValue(faction, out var value);
                if (value > bestValue || (Mathf.Approximately(value, bestValue) && faction < bestFaction))
                {
                    bestFaction = faction;
                    bestValue = value;
                }
            }

            return bestFaction;
        }

        private static bool LabelMatches(string label, string keyword)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(keyword))
                return false;

            return label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float Clamp01(float value)
        {
            return Mathf.Clamp01(Sanitize(value));
        }

        private static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return value;
        }
    }
}
