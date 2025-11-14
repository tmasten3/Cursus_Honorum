using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class PoliticalBehaviorModelTests
    {
        [Test]
        public void FromProfile_AllValuesFinite()
        {
            var character = new Character
            {
                SenatorialInfluence = 10f,
                PopularInfluence = 6f,
                MilitaryInfluence = 4f,
                FamilyInfluence = 2f,
                Courage = 12,
                AmbitionScore = 15,
                Judgment = 11,
                Civic = 9,
                Dignitas = 13,
                Administration = 10
            };

            var profile = character.GetPoliticalProfile();
            var behavior = PoliticalBehaviorModel.FromProfile(profile);

            AssertAllFinite(behavior);
        }

        [Test]
        public void FromProfile_IsStableAcrossCalls()
        {
            var character = new Character
            {
                SenatorialInfluence = 5f,
                PopularInfluence = 7f,
                MilitaryInfluence = 2f,
                Courage = 8,
                AmbitionScore = 9,
                Judgment = 6,
                Civic = 5,
                Dignitas = 7,
                Administration = 6
            };

            var profile = character.GetPoliticalProfile();

            var first = PoliticalBehaviorModel.FromProfile(profile);
            var second = PoliticalBehaviorModel.FromProfile(profile);

            Assert.That(first.Assertiveness, Is.EqualTo(second.Assertiveness));
            Assert.That(first.Stability, Is.EqualTo(second.Stability));
            Assert.That(first.IdeologyConservatism, Is.EqualTo(second.IdeologyConservatism));
            Assert.That(first.IdeologyPopulism, Is.EqualTo(second.IdeologyPopulism));
            Assert.That(first.MilitaryAssertiveness, Is.EqualTo(second.MilitaryAssertiveness));
            Assert.That(first.HonorInclination, Is.EqualTo(second.HonorInclination));
            Assert.That(first.CorruptionRisk, Is.EqualTo(second.CorruptionRisk));
            Assert.That(first.LongTermPlanning, Is.EqualTo(second.LongTermPlanning));
            Assert.That(first.ShortTermOpportunism, Is.EqualTo(second.ShortTermOpportunism));
            Assert.That(first.PowerBaseSenate, Is.EqualTo(second.PowerBaseSenate));
            Assert.That(first.PowerBasePopular, Is.EqualTo(second.PowerBasePopular));
            Assert.That(first.PowerBaseMilitary, Is.EqualTo(second.PowerBaseMilitary));
        }

        [Test]
        public void ZeroedProfile_ProducesNeutralBaseline()
        {
            var character = new Character
            {
                SenatorialInfluence = 0f,
                PopularInfluence = 0f,
                MilitaryInfluence = 0f,
                FamilyInfluence = 0f,
                Courage = 0,
                AmbitionScore = 0,
                Judgment = 0,
                Civic = 0,
                Dignitas = 0,
                Administration = 0
            };

            var behavior = character.GetPoliticalBehavior();

            Assert.That(behavior.Assertiveness, Is.EqualTo(0.5f));
            Assert.That(behavior.Stability, Is.EqualTo(0.5f));
            Assert.That(behavior.IdeologyConservatism, Is.EqualTo(0.5f));
            Assert.That(behavior.IdeologyPopulism, Is.EqualTo(0.5f));
            Assert.That(behavior.MilitaryAssertiveness, Is.EqualTo(0.5f));
            Assert.That(behavior.HonorInclination, Is.EqualTo(0.5f));
            Assert.That(behavior.CorruptionRisk, Is.EqualTo(0.5f));
            Assert.That(behavior.LongTermPlanning, Is.EqualTo(0.5f));
            Assert.That(behavior.ShortTermOpportunism, Is.EqualTo(0.5f));
            Assert.That(behavior.PowerBaseSenate, Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.That(behavior.PowerBasePopular, Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.That(behavior.PowerBaseMilitary, Is.EqualTo(1f / 3f).Within(0.0001f));
        }

        [Test]
        public void StrongSenateProfile_IncreasesConservatism()
        {
            var baselineCharacter = new Character
            {
                SenatorialInfluence = 5f,
                PopularInfluence = 5f,
                MilitaryInfluence = 0f
            };

            var senateHeavyCharacter = new Character
            {
                SenatorialInfluence = 20f,
                PopularInfluence = 2f,
                MilitaryInfluence = 0f
            };

            var baseline = baselineCharacter.GetPoliticalBehavior();
            var senateHeavy = senateHeavyCharacter.GetPoliticalBehavior();

            Assert.That(senateHeavy.IdeologyConservatism, Is.GreaterThan(baseline.IdeologyConservatism));
        }

        [Test]
        public void StrongPopularProfile_IncreasesPopulism()
        {
            var baselineCharacter = new Character
            {
                SenatorialInfluence = 5f,
                PopularInfluence = 5f,
                MilitaryInfluence = 0f
            };

            var popularHeavyCharacter = new Character
            {
                SenatorialInfluence = 2f,
                PopularInfluence = 20f,
                MilitaryInfluence = 0f
            };

            var baseline = baselineCharacter.GetPoliticalBehavior();
            var popularHeavy = popularHeavyCharacter.GetPoliticalBehavior();

            Assert.That(popularHeavy.IdeologyPopulism, Is.GreaterThan(baseline.IdeologyPopulism));
        }

        [Test]
        public void HighDignitasAndCivic_IncreaseHonor()
        {
            var baselineCharacter = new Character
            {
                Dignitas = 0,
                Civic = 0
            };

            var honorableCharacter = new Character
            {
                Dignitas = 18,
                Civic = 17
            };

            var baseline = baselineCharacter.GetPoliticalBehavior();
            var honorable = honorableCharacter.GetPoliticalBehavior();

            Assert.That(honorable.HonorInclination, Is.GreaterThan(baseline.HonorInclination));
        }

        [Test]
        public void CorruptionRisk_IsInverseOfHonor()
        {
            var character = new Character
            {
                Dignitas = 15,
                Civic = 14
            };

            var behavior = character.GetPoliticalBehavior();

            Assert.That(behavior.CorruptionRisk, Is.EqualTo(1f - behavior.HonorInclination).Within(0.0001f));
        }

        [Test]
        public void HighAmbition_IncreasesOpportunism()
        {
            var baselineCharacter = new Character
            {
                AmbitionScore = 0
            };

            var ambitiousCharacter = new Character
            {
                AmbitionScore = 18
            };

            var baseline = baselineCharacter.GetPoliticalBehavior();
            var ambitious = ambitiousCharacter.GetPoliticalBehavior();

            Assert.That(ambitious.ShortTermOpportunism, Is.GreaterThan(baseline.ShortTermOpportunism));
        }

        [Test]
        public void HighJudgmentAndAdministration_IncreaseLongTermPlanning()
        {
            var baselineCharacter = new Character
            {
                Judgment = 0,
                Administration = 0
            };

            var plannerCharacter = new Character
            {
                Judgment = 19,
                Administration = 18
            };

            var baseline = baselineCharacter.GetPoliticalBehavior();
            var planner = plannerCharacter.GetPoliticalBehavior();

            Assert.That(planner.LongTermPlanning, Is.GreaterThan(baseline.LongTermPlanning));
        }

        private static void AssertAllFinite(PoliticalBehaviorModel behavior)
        {
            Assert.That(float.IsNaN(behavior.Assertiveness), Is.False);
            Assert.That(float.IsNaN(behavior.Stability), Is.False);
            Assert.That(float.IsNaN(behavior.IdeologyConservatism), Is.False);
            Assert.That(float.IsNaN(behavior.IdeologyPopulism), Is.False);
            Assert.That(float.IsNaN(behavior.MilitaryAssertiveness), Is.False);
            Assert.That(float.IsNaN(behavior.HonorInclination), Is.False);
            Assert.That(float.IsNaN(behavior.CorruptionRisk), Is.False);
            Assert.That(float.IsNaN(behavior.LongTermPlanning), Is.False);
            Assert.That(float.IsNaN(behavior.ShortTermOpportunism), Is.False);
            Assert.That(float.IsNaN(behavior.PowerBaseSenate), Is.False);
            Assert.That(float.IsNaN(behavior.PowerBasePopular), Is.False);
            Assert.That(float.IsNaN(behavior.PowerBaseMilitary), Is.False);

            Assert.That(float.IsInfinity(behavior.Assertiveness), Is.False);
            Assert.That(float.IsInfinity(behavior.Stability), Is.False);
            Assert.That(float.IsInfinity(behavior.IdeologyConservatism), Is.False);
            Assert.That(float.IsInfinity(behavior.IdeologyPopulism), Is.False);
            Assert.That(float.IsInfinity(behavior.MilitaryAssertiveness), Is.False);
            Assert.That(float.IsInfinity(behavior.HonorInclination), Is.False);
            Assert.That(float.IsInfinity(behavior.CorruptionRisk), Is.False);
            Assert.That(float.IsInfinity(behavior.LongTermPlanning), Is.False);
            Assert.That(float.IsInfinity(behavior.ShortTermOpportunism), Is.False);
            Assert.That(float.IsInfinity(behavior.PowerBaseSenate), Is.False);
            Assert.That(float.IsInfinity(behavior.PowerBasePopular), Is.False);
            Assert.That(float.IsInfinity(behavior.PowerBaseMilitary), Is.False);
        }
    }
}
