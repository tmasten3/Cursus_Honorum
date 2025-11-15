using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;
using Game.Systems.CharacterSystem;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class CharacterRepositoryTests
    {
        [SetUp]
        public void SetUp()
        {
            RomanFamilyRegistry.Reset();
        }

        [Test]
        public void IndexesIncludeCognominaAndBranches()
        {
            var repo = new CharacterRepository();

            var scipioBranch = RomanFamilyRegistry.RegisterOrGet("Cornelius", SocialClass.Patrician, "Scipio", null, false);
            var lentulusBranch = RomanFamilyRegistry.RegisterOrGet("Cornelius", SocialClass.Patrician, "Lentulus", null, false);

            var characters = new List<Character>
            {
                CreateCharacter(1, scipioBranch, Gender.Male, "Lucius"),
                CreateCharacter(2, scipioBranch, Gender.Female, "Claudia"),
                CreateCharacter(3, lentulusBranch, Gender.Male, "Gnaeus")
            };

            foreach (var character in characters)
                repo.Add(character, keepDead: false);

            var scipiones = repo.GetByCognomen("SCIPIO");
            Assert.That(scipiones.Select(c => c.ID), Is.EquivalentTo(new[] { 1, 2 }));

            var branchMembers = repo.GetByBranch(scipioBranch.Id);
            Assert.That(branchMembers.Select(c => c.ID), Is.EquivalentTo(new[] { 1, 2 }));

            var lentuli = repo.GetByCognomen("Lentulus");
            Assert.That(lentuli.Select(c => c.ID), Is.EquivalentTo(new[] { 3 }));

            var missing = repo.GetByBranch("unknown");
            Assert.IsEmpty(missing);
        }

        private static Character CreateCharacter(int id, RomanFamilyBranch branch, Gender gender, string praenomen)
        {
            return new Character
            {
                ID = id,
                Gender = gender,
                BirthYear = -300,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 30,
                IsAlive = true,
                RomanName = new RomanName(praenomen, branch.GensKey, branch.Cognomen, gender),
                Family = branch.GensKey,
                Class = branch.SocialClass,
                BranchId = branch.Id,
                BranchParentId = branch.ParentBranchId,
                BranchDisplayName = branch.DisplayName,
                BranchIsDynamic = branch.IsDynamic,
                TraitRecords = new List<TraitRecord>(),
                CareerMilestones = new List<CareerMilestone>(),
                OfficeHistory = new List<OfficeHistoryEntry>()
            };
        }
    }
}
