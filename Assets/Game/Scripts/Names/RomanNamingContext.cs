using System;
using System.Collections.Generic;

namespace Game.Data.Characters
{
    internal sealed class RomanNamingContext
    {
        public RomanNamingContext(int seed)
        {
            Seed = seed;
            Random = new Random(seed);
            Registry = new RomanGensRegistry(RomanGensData.All);
            BranchRegistry = new RomanBranchRegistry();
            BranchRegistry.Reset();
            InitializeBaseBranches();
        }

        public int Seed { get; }
        public Random Random { get; }
        public RomanGensRegistry Registry { get; }
        public RomanBranchRegistry BranchRegistry { get; }

        private void InitializeBaseBranches()
        {
            foreach (var definition in Registry.Definitions)
            {
                foreach (var variant in definition.Variants)
                {
                    var baseCognomina = variant.BaseCognomina;
                    if (baseCognomina.Count == 0)
                    {
                        // Create a few seed branches for variants without documented cognomina.
                        int supplemental = variant.SocialClass == SocialClass.Patrician ? 2 : 3;
                        for (int i = 0; i < supplemental; i++)
                        {
                            BranchRegistry.RegisterBranch(variant, GenerateFallbackCognomen(variant, i), true, null);
                        }
                        continue;
                    }

                    foreach (var cognomen in baseCognomina)
                    {
                        BranchRegistry.RegisterBranch(variant, cognomen, false, null);
                    }
                }
            }
        }

        private string GenerateFallbackCognomen(RomanGensVariant variant, int index)
        {
            var stem = RomanNameUtility.Normalize(variant.Definition.StylizedNomen) ?? "Roman";
            if (stem.EndsWith("us", StringComparison.OrdinalIgnoreCase))
                stem = stem[..^2];
            return $"{stem}ianus{index + 1}";
        }
    }
}
