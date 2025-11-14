using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Read-only aggregation that formats political tendencies for developer-facing tools.
    /// </summary>
    public readonly struct CharacterPoliticalSummary
    {
        public float TotalInfluence { get; }
        public float SenateLean { get; }
        public float PopularLean { get; }
        public float MilitaryLean { get; }
        public float FamilyLean { get; }

        public string IdeologyLabel { get; }
        public string TemperamentLabel { get; }
        public string HonorLabel { get; }
        public string AmbitionLabel { get; }
        public string PowerBaseLabel { get; }

        public FactionType Faction { get; }

        private CharacterPoliticalSummary(
            float totalInfluence,
            float senateLean,
            float popularLean,
            float militaryLean,
            float familyLean,
            string ideologyLabel,
            string temperamentLabel,
            string honorLabel,
            string ambitionLabel,
            string powerBaseLabel,
            FactionType faction)
        {
            TotalInfluence = SanitizeValue(totalInfluence);
            SenateLean = Mathf.Clamp01(SanitizeValue(senateLean));
            PopularLean = Mathf.Clamp01(SanitizeValue(popularLean));
            MilitaryLean = Mathf.Clamp01(SanitizeValue(militaryLean));
            FamilyLean = Mathf.Clamp01(SanitizeValue(familyLean));
            IdeologyLabel = ideologyLabel ?? string.Empty;
            TemperamentLabel = temperamentLabel ?? string.Empty;
            HonorLabel = honorLabel ?? string.Empty;
            AmbitionLabel = ambitionLabel ?? string.Empty;
            PowerBaseLabel = powerBaseLabel ?? string.Empty;
            Faction = faction;
        }

        public static CharacterPoliticalSummary FromProfileAndBehavior(
            PoliticalProfile profile,
            PoliticalBehaviorModel behavior)
        {
            var totalInfluence = profile.TotalInfluence;
            var senateLean = profile.SenateLean;
            var popularLean = profile.PopularLean;
            var militaryLean = profile.MilitaryLean;
            var familyLean = profile.FamilyLean;

            var ideologyLabel = DetermineIdeologyLabel(senateLean, popularLean);
            var temperamentLabel = DetermineTemperamentLabel(behavior.Assertiveness, behavior.Stability);
            var honorLabel = DetermineHonorLabel(behavior.HonorInclination);
            var ambitionLabel = DetermineAmbitionLabel(behavior.ShortTermOpportunism);
            var powerBaseLabel = DeterminePowerBaseLabel(
                behavior.PowerBaseSenate,
                behavior.PowerBasePopular,
                behavior.PowerBaseMilitary);

            return new CharacterPoliticalSummary(
                totalInfluence,
                senateLean,
                popularLean,
                militaryLean,
                familyLean,
                ideologyLabel,
                temperamentLabel,
                honorLabel,
                ambitionLabel,
                powerBaseLabel,
                profile.PrimaryFaction);
        }

        private static string DetermineIdeologyLabel(float senateLean, float popularLean)
        {
            if (senateLean > 0.6f)
                return "Optimate-leaning";

            if (popularLean > 0.6f)
                return "Populares-leaning";

            return "Mixed Alignment";
        }

        private static string DetermineTemperamentLabel(float assertiveness, float stability)
        {
            if (assertiveness > 0.6f && stability > 0.6f)
                return "Steady Leadership";

            if (assertiveness > 0.6f && stability <= 0.6f)
                return "Forceful";

            if (assertiveness <= 0.6f && stability > 0.6f)
                return "Cautious";

            return "Unpredictable";
        }

        private static string DetermineHonorLabel(float honorInclination)
        {
            if (honorInclination > 0.65f)
                return "Honorbound";

            if (honorInclination < 0.35f)
                return "Corruption-prone";

            return "Pragmatic";
        }

        private static string DetermineAmbitionLabel(float opportunism)
        {
            if (opportunism > 0.65f)
                return "Highly Ambitious";

            if (opportunism < 0.35f)
                return "Low Ambition";

            return "Moderate Ambition";
        }

        private static string DeterminePowerBaseLabel(float senate, float popular, float military)
        {
            var sanitizedSenate = Mathf.Clamp01(SanitizeValue(senate));
            var sanitizedPopular = Mathf.Clamp01(SanitizeValue(popular));
            var sanitizedMilitary = Mathf.Clamp01(SanitizeValue(military));

            var max = Mathf.Max(sanitizedSenate, Mathf.Max(sanitizedPopular, sanitizedMilitary));
            var matches = CountMatches(max, sanitizedSenate, sanitizedPopular, sanitizedMilitary);

            if (matches > 1)
                return "Diffuse Power Base";

            if (Approximately(sanitizedSenate, max))
                return "Senatorial";

            if (Approximately(sanitizedPopular, max))
                return "Popular Support";

            if (Approximately(sanitizedMilitary, max))
                return "Military Backing";

            return "Diffuse Power Base";
        }

        private static int CountMatches(float max, params float[] values)
        {
            var count = 0;
            foreach (var value in values)
            {
                if (Approximately(value, max))
                    count++;
            }

            return count;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static float SanitizeValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return value;
        }
    }
}
