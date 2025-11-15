using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;
using Game.Systems.Characters.Generation;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class BaseCharactersGeneratorTests
    {
        [Test]
        public void GeneratorProducesPopulationWithBranches()
        {
            RomanFamilyRegistry.Reset();

            var generator = new BaseCharactersGenerator(seed: 4001, startYear: -248);
            var population = generator.Generate();

            Assert.That(population, Is.Not.Null);
            Assert.That(population.Count, Is.InRange(1200, 2000),
                "Base population should fall within the designed size range.");
            Assert.IsTrue(population.All(c => c != null && c.RomanName != null),
                "Every generated character should include a Roman identity.");

            var branchIds = new HashSet<string>(population
                .Where(c => !string.IsNullOrWhiteSpace(c.BranchId))
                .Select(c => c.BranchId));

            Assert.That(branchIds.Count, Is.GreaterThanOrEqualTo(60),
                "Generator should seed dozens of active cognomen branches across the gens roster.");

            var registryBranches = RomanFamilyRegistry.GetAllBranches().ToList();
            Assert.That(registryBranches.Count, Is.EqualTo(branchIds.Count),
                "Registered branch catalogue should match the set referenced by characters.");
            CollectionAssert.AreEquivalent(branchIds, registryBranches.Select(b => b.Id));
            Assert.IsTrue(registryBranches.Any(b => b.IsDynamic),
                "At least one dynamic branch should be generated for cognomen-less gentes.");
        }
    }
}
