using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class PoliticalProfileTests
    {
        [Test]
        public void FromCharacter_CopiesCoreValues()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 5f,
                MilitaryInfluence = 2f,
                FamilyInfluence = 3f,
                Civic = 14,
                AmbitionScore = 12,
                Courage = 9,
                Dignitas = 11,
                Administration = 8,
                Judgment = 6,
                Faction = FactionType.Optimates
            };

            var profile = PoliticalProfile.FromCharacter(character);

            Assert.That(profile.SenatorialInfluence, Is.EqualTo(10f));
            Assert.That(profile.PopularInfluence, Is.EqualTo(5f));
            Assert.That(profile.MilitaryInfluence, Is.EqualTo(2f));
            Assert.That(profile.FamilyInfluence, Is.EqualTo(3f));
            Assert.That(profile.TotalInfluence, Is.EqualTo(20f));
            Assert.That(profile.Civic, Is.EqualTo(14));
            Assert.That(profile.AmbitionScore, Is.EqualTo(12));
            Assert.That(profile.Courage, Is.EqualTo(9));
            Assert.That(profile.Dignitas, Is.EqualTo(11));
            Assert.That(profile.Administration, Is.EqualTo(8));
            Assert.That(profile.Judgment, Is.EqualTo(6));
            Assert.That(profile.Faction, Is.EqualTo(FactionType.Optimates));
            Assert.That(profile.PrimaryFaction, Is.EqualTo(FactionType.Optimates));
            Assert.That(profile.SenateLean, Is.EqualTo(10f / (10f + 5f)).Within(0.0001f));
            Assert.That(profile.PopularLean, Is.EqualTo(5f / 20f).Within(0.0001f));
            Assert.That(profile.MilitaryLean, Is.EqualTo(2f / 20f).Within(0.0001f));
            Assert.That(profile.FamilyLean, Is.EqualTo(3f / 20f).Within(0.0001f));
        }

        [Test]
        public void FromCharacter_WithZeroInfluencePools_ProducesNeutralLeans()
        {
            var character = new Character
            {
                SenatorialInfluence = 0f,
                PopularInfluence = 0f,
                MilitaryInfluence = 0f,
                FamilyInfluence = 0f,
                Faction = FactionType.Neutral
            };

            var profile = PoliticalProfile.FromCharacter(character);

            Assert.That(profile.TotalInfluence, Is.EqualTo(0f));
            Assert.That(profile.SenateLean, Is.EqualTo(0.5f));
            Assert.That(profile.PopularLean, Is.EqualTo(0f));
            Assert.That(profile.MilitaryLean, Is.EqualTo(0f));
            Assert.That(profile.FamilyLean, Is.EqualTo(0f));
        }

        [Test]
        public void FromCharacter_WithAllInfluencePoolsNonZero_NormalizesLeans()
        {
            var character = new Character
            {
                SenatorialInfluence = 4f,
                PopularInfluence = 3f,
                MilitaryInfluence = 2f,
                FamilyInfluence = 1f,
                Faction = FactionType.Populares
            };

            var profile = PoliticalProfile.FromCharacter(character);

            Assert.That(profile.TotalInfluence, Is.EqualTo(10f));
            Assert.That(profile.SenateLean, Is.EqualTo(4f / (4f + 3f)).Within(0.0001f));
            Assert.That(profile.PopularLean, Is.EqualTo(3f / 10f).Within(0.0001f));
            Assert.That(profile.MilitaryLean, Is.EqualTo(2f / 10f).Within(0.0001f));
            Assert.That(profile.FamilyLean, Is.EqualTo(1f / 10f).Within(0.0001f));
        }

        [Test]
        public void LeansAreAlwaysFinite()
        {
            var character = new Character
            {
                SenatorialInfluence = 0f,
                PopularInfluence = 0f,
                MilitaryInfluence = 0f,
                FamilyInfluence = 0f
            };

            var profile = PoliticalProfile.FromCharacter(character);

            Assert.That(float.IsNaN(profile.SenateLean), Is.False);
            Assert.That(float.IsNaN(profile.PopularLean), Is.False);
            Assert.That(float.IsNaN(profile.MilitaryLean), Is.False);
            Assert.That(float.IsNaN(profile.FamilyLean), Is.False);
            Assert.That(float.IsInfinity(profile.SenateLean), Is.False);
            Assert.That(float.IsInfinity(profile.PopularLean), Is.False);
            Assert.That(float.IsInfinity(profile.MilitaryLean), Is.False);
            Assert.That(float.IsInfinity(profile.FamilyLean), Is.False);
        }

        [Test]
        public void InvalidFactionIsNormalizedBeforeProfileCreation()
        {
            var character = new Character
            {
                Faction = (FactionType)999,
                SenatorialInfluence = 1f,
                PopularInfluence = 1f,
                MilitaryInfluence = 1f,
                FamilyInfluence = 1f
            };

            CharacterFactory.NormalizeDeserializedCharacter(character, "Test");

            var profile = PoliticalProfile.FromCharacter(character);

            Assert.That(profile.Faction, Is.EqualTo(FactionType.Neutral));
            Assert.That(profile.PrimaryFaction, Is.EqualTo(FactionType.Neutral));
        }
    }
}
