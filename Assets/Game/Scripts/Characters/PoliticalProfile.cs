using System;
using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Aggregates political data for a character into a normalized, read-only view.
    /// </summary>
    public readonly struct PoliticalProfile
    {
        private const int MinPoliticalStatValue = 0;
        private const int MaxPoliticalStatValue = 20;
        private const int MinSkillValue = 0;
        private const int MaxSkillValue = 20;

        public float SenatorialInfluence { get; }
        public float PopularInfluence { get; }
        public float MilitaryInfluence { get; }
        public float FamilyInfluence { get; }
        public float TotalInfluence => Mathf.Max(0f,
            SenatorialInfluence + PopularInfluence + MilitaryInfluence + FamilyInfluence);

        public int Oratory { get; }
        public int AmbitionScore { get; }
        public int Courage { get; }
        public int Dignitas { get; }

        public int Administration { get; }
        public int Judgment { get; }
        public int Strategy { get; }
        public int Civic { get; }

        public FactionType Faction { get; }

        public float SenateLean
        {
            get
            {
                var combined = SenatorialInfluence + PopularInfluence;
                if (combined <= 0f)
                    return 0.5f;

                if (SenatorialInfluence <= 0f)
                    return 0f;

                return Mathf.Clamp01(SenatorialInfluence / combined);
            }
        }

        public float PopularLean => SafeRatio(PopularInfluence, TotalInfluence);
        public float MilitaryLean => SafeRatio(MilitaryInfluence, TotalInfluence);
        public float FamilyLean => SafeRatio(FamilyInfluence, TotalInfluence);
        public FactionType PrimaryFaction => Faction;

        private PoliticalProfile(
            float senatorialInfluence,
            float popularInfluence,
            float militaryInfluence,
            float familyInfluence,
            int oratory,
            int ambitionScore,
            int courage,
            int dignitas,
            int administration,
            int judgment,
            int strategy,
            int civic,
            FactionType faction)
        {
            SenatorialInfluence = Mathf.Max(0f, senatorialInfluence);
            PopularInfluence = Mathf.Max(0f, popularInfluence);
            MilitaryInfluence = Mathf.Max(0f, militaryInfluence);
            FamilyInfluence = Mathf.Max(0f, familyInfluence);
            Oratory = Mathf.Clamp(oratory, MinPoliticalStatValue, MaxPoliticalStatValue);
            AmbitionScore = Mathf.Clamp(ambitionScore, MinPoliticalStatValue, MaxPoliticalStatValue);
            Courage = Mathf.Clamp(courage, MinPoliticalStatValue, MaxPoliticalStatValue);
            Dignitas = Mathf.Clamp(dignitas, MinPoliticalStatValue, MaxPoliticalStatValue);
            Administration = Mathf.Clamp(administration, MinSkillValue, MaxSkillValue);
            Judgment = Mathf.Clamp(judgment, MinSkillValue, MaxSkillValue);
            Strategy = Mathf.Clamp(strategy, MinSkillValue, MaxSkillValue);
            Civic = Mathf.Clamp(civic, MinSkillValue, MaxSkillValue);
            Faction = faction;
        }

        public static PoliticalProfile FromCharacter(Character character)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            float senatorial = SanitizeInfluence(character.SenatorialInfluence);
            float popular = SanitizeInfluence(character.PopularInfluence);
            float military = SanitizeInfluence(character.MilitaryInfluence);
            float family = SanitizeInfluence(character.FamilyInfluence);

            var faction = Enum.IsDefined(typeof(FactionType), character.Faction)
                ? character.Faction
                : FactionType.Neutral;

            return new PoliticalProfile(
                senatorial,
                popular,
                military,
                family,
                character.Oratory,
                character.AmbitionScore,
                character.Courage,
                character.Dignitas,
                character.Administration,
                character.Judgment,
                character.Strategy,
                character.Civic,
                faction);
        }

        private static float SafeRatio(float value, float total)
        {
            if (total <= 0f || value <= 0f)
                return 0f;

            var ratio = value / total;
            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
                return 0f;

            return Mathf.Clamp01(ratio);
        }

        private static float SanitizeInfluence(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return 0f;

            return value;
        }
    }
}
