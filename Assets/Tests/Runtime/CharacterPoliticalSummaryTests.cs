using System;
using System.Reflection;
using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class CharacterPoliticalSummaryTests
    {
        [Test]
        public void SenateLeaningCharacter_ProducesOptimateLabel()
        {
            var character = new Character
            {
                SenatorialInfluence = 70f,
                PopularInfluence = 10f,
                MilitaryInfluence = 5f,
                FamilyInfluence = 5f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.IdeologyLabel, Is.EqualTo("Optimate-leaning"));
        }

        [Test]
        public void PopularLeaningCharacter_ProducesPopularesLabel()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 80f,
                MilitaryInfluence = 5f,
                FamilyInfluence = 5f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.IdeologyLabel, Is.EqualTo("Populares-leaning"));
        }

        [Test]
        public void BalancedCharacter_ProducesMixedAlignment()
        {
            var character = new Character
            {
                SenatorialInfluence = 30f,
                PopularInfluence = 30f,
                MilitaryInfluence = 20f,
                FamilyInfluence = 20f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.IdeologyLabel, Is.EqualTo("Mixed Alignment"));
        }

        [Test]
        public void HighAssertivenessAndStability_ProduceSteadyLeadership()
        {
            var character = new Character
            {
                Courage = 20,
                AmbitionScore = 20,
                Judgment = 20,
                Civic = 20
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.TemperamentLabel, Is.EqualTo("Steady Leadership"));
        }

        [Test]
        public void LowAssertivenessHighStability_ProducesCautious()
        {
            var character = new Character
            {
                Courage = 0,
                AmbitionScore = 0,
                Judgment = 20,
                Civic = 20
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.TemperamentLabel, Is.EqualTo("Cautious"));
        }

        [Test]
        public void HighHonor_ProducesHonorbound()
        {
            var character = new Character
            {
                Dignitas = 20,
                Civic = 20
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.HonorLabel, Is.EqualTo("Honorbound"));
        }

        [Test]
        public void LowHonor_ProducesCorruptionProne()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 10f,
                MilitaryInfluence = 10f,
                FamilyInfluence = 10f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = CreateBehavior(honorInclination: 0.2f);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.HonorLabel, Is.EqualTo("Corruption-prone"));
        }

        [Test]
        public void MiddleHonor_ProducesPragmatic()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 10f,
                MilitaryInfluence = 10f,
                FamilyInfluence = 10f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = CreateBehavior(honorInclination: 0.5f);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.HonorLabel, Is.EqualTo("Pragmatic"));
        }

        [Test]
        public void HighOpportunism_ProducesHighlyAmbitious()
        {
            var character = new Character
            {
                AmbitionScore = 20
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.AmbitionLabel, Is.EqualTo("Highly Ambitious"));
        }

        [Test]
        public void DominantPowerBase_SelectsCorrectLabel()
        {
            var character = new Character
            {
                SenatorialInfluence = 50f,
                PopularInfluence = 30f,
                MilitaryInfluence = 20f
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.PowerBaseLabel, Is.EqualTo("Senatorial"));
        }

        [Test]
        public void SummaryOutputs_AreNonNullAndFinite()
        {
            var character = new Character
            {
                SenatorialInfluence = 25f,
                PopularInfluence = 35f,
                MilitaryInfluence = 15f,
                FamilyInfluence = 25f,
                Courage = 12,
                AmbitionScore = 14,
                Judgment = 13,
                Civic = 11,
                Dignitas = 10
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);
            var summary = CharacterPoliticalSummary.FromProfileAndBehavior(profile, behavior);

            Assert.That(summary.IdeologyLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(summary.TemperamentLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(summary.HonorLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(summary.AmbitionLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(summary.PowerBaseLabel, Is.Not.Null.And.Not.Empty);

            Assert.That(float.IsNaN(summary.TotalInfluence) || float.IsInfinity(summary.TotalInfluence), Is.False);
            Assert.That(float.IsNaN(summary.SenateLean) || float.IsInfinity(summary.SenateLean), Is.False);
            Assert.That(float.IsNaN(summary.PopularLean) || float.IsInfinity(summary.PopularLean), Is.False);
            Assert.That(float.IsNaN(summary.MilitaryLean) || float.IsInfinity(summary.MilitaryLean), Is.False);
            Assert.That(float.IsNaN(summary.FamilyLean) || float.IsInfinity(summary.FamilyLean), Is.False);
        }

        private static PoliticalBehaviorModel CreateBehavior(
            float assertiveness = 0.5f,
            float stability = 0.5f,
            float honorInclination = 0.5f,
            float opportunism = 0.5f,
            float powerBaseSenate = 1f / 3f,
            float powerBasePopular = 1f / 3f,
            float powerBaseMilitary = 1f / 3f)
        {
            var behavior = default(PoliticalBehaviorModel);

            SetField(ref behavior, AssertivenessField, assertiveness);
            SetField(ref behavior, StabilityField, stability);
            SetField(ref behavior, HonorField, honorInclination);
            SetField(ref behavior, OpportunismField, opportunism);
            SetField(ref behavior, PowerBaseSenateField, powerBaseSenate);
            SetField(ref behavior, PowerBasePopularField, powerBasePopular);
            SetField(ref behavior, PowerBaseMilitaryField, powerBaseMilitary);

            return behavior;
        }

        private static void SetField(ref PoliticalBehaviorModel behavior, FieldInfo field, float value)
        {
            object boxed = behavior;
            field.SetValue(boxed, value);
            behavior = (PoliticalBehaviorModel)boxed;
        }

        private static readonly FieldInfo AssertivenessField = typeof(PoliticalBehaviorModel)
            .GetField("<Assertiveness>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Assertiveness backing field not found.");

        private static readonly FieldInfo StabilityField = typeof(PoliticalBehaviorModel)
            .GetField("<Stability>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Stability backing field not found.");

        private static readonly FieldInfo HonorField = typeof(PoliticalBehaviorModel)
            .GetField("<HonorInclination>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HonorInclination backing field not found.");

        private static readonly FieldInfo OpportunismField = typeof(PoliticalBehaviorModel)
            .GetField("<ShortTermOpportunism>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ShortTermOpportunism backing field not found.");

        private static readonly FieldInfo PowerBaseSenateField = typeof(PoliticalBehaviorModel)
            .GetField("<PowerBaseSenate>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PowerBaseSenate backing field not found.");

        private static readonly FieldInfo PowerBasePopularField = typeof(PoliticalBehaviorModel)
            .GetField("<PowerBasePopular>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PowerBasePopular backing field not found.");

        private static readonly FieldInfo PowerBaseMilitaryField = typeof(PoliticalBehaviorModel)
            .GetField("<PowerBaseMilitary>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PowerBaseMilitary backing field not found.");
    }
}
