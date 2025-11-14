using System.Reflection;
using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Simulation
{
    public class FactionAffinityModelTests
    {
        [Test]
        public void FiniteValueTest()
        {
            var character = new Character
            {
                SenatorialInfluence = 14f,
                PopularInfluence = 9f,
                MilitaryInfluence = 5f,
                FamilyInfluence = 4f,
                AmbitionScore = 12,
                Courage = 11,
                Dignitas = 13,
                Judgment = 12,
                Civic = 10,
                Administration = 11
            };

            var model = BuildModel(character);

            var sum = 0f;
            foreach (var kvp in model.AffinityByFaction)
            {
                Assert.That(kvp.Value, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
                Assert.That(!float.IsNaN(kvp.Value) && !float.IsInfinity(kvp.Value));
                sum += kvp.Value;
            }

            Assert.That(sum, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void DominantLean_SenateProducesOptimates()
        {
            var character = new Character
            {
                SenatorialInfluence = 40f,
                PopularInfluence = 5f,
                MilitaryInfluence = 2f,
                FamilyInfluence = 3f,
                Dignitas = 16,
                Civic = 14,
                AmbitionScore = 8,
                Courage = 9
            };

            var model = BuildModel(character);

            Assert.That(model.PrimaryAffinity, Is.EqualTo(FactionType.Optimates));
        }

        [Test]
        public void DominantLean_PopularProducesPopulares()
        {
            var character = new Character
            {
                SenatorialInfluence = 5f,
                PopularInfluence = 35f,
                MilitaryInfluence = 4f,
                FamilyInfluence = 2f,
                AmbitionScore = 16,
                Courage = 8,
                Dignitas = 6
            };

            var model = BuildModel(character);

            Assert.That(model.PrimaryAffinity, Is.EqualTo(FactionType.Populares));
        }

        [Test]
        public void DominantLean_MilitaryProducesMilitarists()
        {
            var character = new Character
            {
                SenatorialInfluence = 4f,
                PopularInfluence = 6f,
                MilitaryInfluence = 30f,
                FamilyInfluence = 2f,
                Courage = 18,
                AmbitionScore = 10,
                Dignitas = 7
            };

            var model = BuildModel(character);

            Assert.That(model.PrimaryAffinity, Is.EqualTo(FactionType.Militarists));
        }

        [Test]
        public void BalancedInputs_FavorModerateOrNeutral()
        {
            var character = new Character
            {
                SenatorialInfluence = 12f,
                PopularInfluence = 11f,
                MilitaryInfluence = 10f,
                FamilyInfluence = 9f,
                AmbitionScore = 9,
                Courage = 9,
                Dignitas = 11,
                Judgment = 12,
                Civic = 12,
                Administration = 11
            };

            var model = BuildModel(character);

            Assert.That(model.PrimaryAffinity, Is.EqualTo(FactionType.Moderates).Or.EqualTo(FactionType.Neutral));
        }

        [Test]
        public void SummaryLabel_OptimateIncreasesAffinity()
        {
            var character = new Character
            {
                SenatorialInfluence = 12f,
                PopularInfluence = 9f,
                MilitaryInfluence = 5f,
                FamilyInfluence = 3f,
                AmbitionScore = 9,
                Courage = 10,
                Dignitas = 12,
                Civic = 11,
                Judgment = 10,
                Administration = 10
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);
            var optimateSummary = OverrideIdeologyLabel(summary, "Optimate-leaning");

            var neutralModel = FactionAffinityModel.FromPoliticalData(profile, behavior, summary);
            var optimateModel = FactionAffinityModel.FromPoliticalData(profile, behavior, optimateSummary);

            Assert.That(optimateModel.GetAffinity(FactionType.Optimates),
                Is.GreaterThan(neutralModel.GetAffinity(FactionType.Optimates)));
        }

        [Test]
        public void Normalization_SumEqualsOne()
        {
            var character = new Character
            {
                SenatorialInfluence = 8f,
                PopularInfluence = 12f,
                MilitaryInfluence = 6f,
                FamilyInfluence = 4f,
                AmbitionScore = 13,
                Courage = 9,
                Dignitas = 8,
                Civic = 9,
                Judgment = 8
            };

            var model = BuildModel(character);

            var sum = 0f;
            foreach (var kvp in model.AffinityByFaction)
            {
                sum += kvp.Value;
            }

            Assert.That(sum, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Determinism_SameInputsProduceIdenticalAffinities()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 10f,
                MilitaryInfluence = 10f,
                FamilyInfluence = 10f,
                AmbitionScore = 10,
                Courage = 10,
                Dignitas = 10,
                Civic = 10,
                Judgment = 10,
                Administration = 10
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            var first = FactionAffinityModel.FromPoliticalData(profile, behavior, summary);
            var second = FactionAffinityModel.FromPoliticalData(profile, behavior, summary);

            foreach (var faction in first.AffinityByFaction.Keys)
            {
                Assert.That(first.GetAffinity(faction), Is.EqualTo(second.GetAffinity(faction)).Within(0.000001f));
            }
        }

        private static FactionAffinityModel BuildModel(Character character)
        {
            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);
            return FactionAffinityModel.FromPoliticalData(profile, behavior, summary);
        }

        private static CharacterPoliticalSummary OverrideIdeologyLabel(CharacterPoliticalSummary original, string label)
        {
            var ctor = typeof(CharacterPoliticalSummary).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(float), typeof(float), typeof(float), typeof(float), typeof(float),
                    typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(FactionType)
                },
                null);

            var parameters = new object[]
            {
                original.TotalInfluence,
                original.SenateLean,
                original.PopularLean,
                original.MilitaryLean,
                original.FamilyLean,
                label,
                original.TemperamentLabel,
                original.HonorLabel,
                original.AmbitionLabel,
                original.PowerBaseLabel,
                original.Faction
            };

            return (CharacterPoliticalSummary)ctor.Invoke(parameters);
        }
    }
}
