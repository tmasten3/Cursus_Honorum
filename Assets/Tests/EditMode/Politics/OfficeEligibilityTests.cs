using System.Collections.Generic;
using Game.Data.Characters;
using Game.Systems.Politics.Offices;
using NUnit.Framework;

namespace Game.Tests.Politics
{
    public class OfficeEligibilityTests
    {
        private OfficeStateService state;
        private OfficeEligibilityService eligibility;

        [SetUp]
        public void SetUp()
        {
            state = new OfficeStateService(null);
            eligibility = new OfficeEligibilityService(state);
        }

        [Test]
        public void AgeRequirement_Is_Enforced()
        {
            var definition = new OfficeDefinition { Id = "quaestor", MinAge = 30 };
            var character = CreateCharacter(1, age: 27);

            bool eligible = eligibility.IsEligible(character, definition, year: 300, out var reason);

            Assert.IsFalse(eligible);
            Assert.AreEqual("Too young", reason);
        }

        [Test]
        public void PrerequisiteOffice_Is_Required()
        {
            var prerequisite = new OfficeDefinition { Id = "quaestor", MinAge = 30 };
            var target = new OfficeDefinition
            {
                Id = "praetor",
                MinAge = 39,
                PrerequisitesAll = new List<string> { "quaestor" }
            };

            var candidate = CreateCharacter(2, age: 42);

            bool initial = eligibility.IsEligible(candidate, target, year: 310, out var initialReason);
            Assert.IsFalse(initial);
            Assert.AreEqual("Requires prior service as quaestor", initialReason);

            state.AddHistorySeed(candidate.ID, prerequisite.Id, seatIndex: 0, startYear: 300, endYear: 300);

            bool eligibleNow = eligibility.IsEligible(candidate, target, year: 312, out var laterReason);
            Assert.IsTrue(eligibleNow, laterReason);
        }

        [Test]
        public void ReelectionCooldown_Is_Enforced()
        {
            var definition = new OfficeDefinition
            {
                Id = "consul",
                MinAge = 42,
                ReelectionGapYears = 10
            };

            var candidate = CreateCharacter(3, age: 50);

            state.AddHistorySeed(candidate.ID, definition.Id, seatIndex: 0, startYear: 300, endYear: 300);

            bool eligible = eligibility.IsEligible(candidate, definition, year: 305, out var reason);

            Assert.IsFalse(eligible);
            StringAssert.Contains("Must wait", reason);
        }

        [Test]
        public void IneligibleCandidates_AreFiltered_Out()
        {
            var quaestor = new OfficeDefinition { Id = "quaestor", MinAge = 30, Rank = 1 };
            var consul = new OfficeDefinition { Id = "consul", MinAge = 42, Rank = 5 };
            var definitions = new List<OfficeDefinition> { quaestor, consul };

            var young = CreateCharacter(10, age: 28);
            var senior = CreateCharacter(11, age: 45);

            var youngEligible = eligibility.GetEligibleOffices(young, definitions, year: 320);
            CollectionAssert.IsEmpty(youngEligible);

            var seniorEligible = eligibility.GetEligibleOffices(senior, definitions, year: 320);
            CollectionAssert.AreEqual(new[] { quaestor, consul }, seniorEligible);
        }

        private static Character CreateCharacter(int id, int age)
        {
            return new Character
            {
                ID = id,
                Age = age,
                Gender = Gender.Male,
                Class = SocialClass.Patrician,
                Influence = 20,
                Wealth = 15,
                IsAlive = true,
                Traits = new List<string>()
            };
        }
    }
}
