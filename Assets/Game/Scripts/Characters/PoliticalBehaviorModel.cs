using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Deterministic descriptor of how a character behaves politically based on their profile data.
    /// </summary>
    public readonly struct PoliticalBehaviorModel
    {
        public float Assertiveness { get; }
        public float Stability { get; }
        public float IdeologyConservatism { get; }
        public float IdeologyPopulism { get; }
        public float MilitaryAssertiveness { get; }
        public float HonorInclination { get; }
        public float CorruptionRisk { get; }
        public float LongTermPlanning { get; }
        public float ShortTermOpportunism { get; }
        public float PowerBaseSenate { get; }
        public float PowerBasePopular { get; }
        public float PowerBaseMilitary { get; }

        private PoliticalBehaviorModel(
            float assertiveness,
            float stability,
            float ideologyConservatism,
            float ideologyPopulism,
            float militaryAssertiveness,
            float honorInclination,
            float corruptionRisk,
            float longTermPlanning,
            float shortTermOpportunism,
            float powerBaseSenate,
            float powerBasePopular,
            float powerBaseMilitary)
        {
            Assertiveness = assertiveness;
            Stability = stability;
            IdeologyConservatism = ideologyConservatism;
            IdeologyPopulism = ideologyPopulism;
            MilitaryAssertiveness = militaryAssertiveness;
            HonorInclination = honorInclination;
            CorruptionRisk = corruptionRisk;
            LongTermPlanning = longTermPlanning;
            ShortTermOpportunism = shortTermOpportunism;
            PowerBaseSenate = powerBaseSenate;
            PowerBasePopular = powerBasePopular;
            PowerBaseMilitary = powerBaseMilitary;
        }

        public static PoliticalBehaviorModel FromProfile(PoliticalProfile profile)
        {
            var courage = NormalizeStat(profile.Courage);
            var ambition = NormalizeStat(profile.AmbitionScore);
            var judgment = NormalizeStat(profile.Judgment);
            var civic = NormalizeStat(profile.Civic);
            var dignitas = NormalizeStat(profile.Dignitas);
            var administration = NormalizeStat(profile.Administration);
            var militaryLean = Mathf.Clamp01(profile.MilitaryLean);

            var assertiveness = BlendWithNeutral((courage + ambition) * 0.5f);
            var stability = BlendWithNeutral((judgment + civic) * 0.5f);
            var ideologyConservatism = ComputeIdeologyValue(profile.SenatorialInfluence, profile.PopularInfluence);
            var ideologyPopulism = ComputeIdeologyValue(profile.PopularInfluence, profile.SenatorialInfluence);
            var militaryAssertiveness = BlendWithNeutral((militaryLean + courage) * 0.5f);
            var honorInclination = BlendWithNeutral((dignitas + civic) * 0.5f);
            var corruptionRisk = Mathf.Clamp01(1f - honorInclination);
            var longTermPlanning = BlendWithNeutral((judgment + administration) * 0.5f);
            var shortTermOpportunism = BlendWithNeutral(ambition);

            var (powerBaseSenate, powerBasePopular, powerBaseMilitary) = ComputePowerBases(
                profile.SenatorialInfluence,
                profile.PopularInfluence,
                profile.MilitaryInfluence);

            return new PoliticalBehaviorModel(
                assertiveness,
                stability,
                ideologyConservatism,
                ideologyPopulism,
                militaryAssertiveness,
                honorInclination,
                corruptionRisk,
                longTermPlanning,
                shortTermOpportunism,
                powerBaseSenate,
                powerBasePopular,
                powerBaseMilitary);
        }

        private static float NormalizeStat(int value)
        {
            var clamped = Mathf.Clamp(value, 0, 20);
            return clamped / 20f;
        }

        private static float BlendWithNeutral(float normalized)
        {
            if (float.IsNaN(normalized) || float.IsInfinity(normalized))
                return 0.5f;

            return Mathf.Clamp01(0.5f + Mathf.Clamp01(normalized) * 0.5f);
        }

        private static float ComputeIdeologyValue(float primaryInfluence, float opposingInfluence)
        {
            var primary = Mathf.Max(0f, primaryInfluence);
            var opposing = Mathf.Max(0f, opposingInfluence);

            var combined = primary + opposing;
            if (combined <= 0f)
                return 0.5f;

            var delta = Mathf.Clamp((primary - opposing) / combined, -1f, 1f);
            return Mathf.Clamp01(0.5f + delta * 0.5f);
        }

        private static (float senate, float popular, float military) ComputePowerBases(
            float senatorialInfluence,
            float popularInfluence,
            float militaryInfluence)
        {
            var senate = Mathf.Max(0f, senatorialInfluence);
            var popular = Mathf.Max(0f, popularInfluence);
            var military = Mathf.Max(0f, militaryInfluence);

            var total = senate + popular + military;
            if (total <= 0f)
                return (1f / 3f, 1f / 3f, 1f / 3f);

            return (senate / total, popular / total, military / total);
        }
    }
}
