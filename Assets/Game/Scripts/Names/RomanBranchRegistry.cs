using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters
{
    /// <summary>
    /// Tracks all cognomen branches for gens variants and supports lookup/creation.
    /// </summary>
    internal sealed class RomanBranchRegistry
    {
        private readonly Dictionary<string, RomanFamilyBranch> byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, RomanFamilyBranch>> byVariantAndCognomen = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<SocialClass, List<RomanFamilyBranch>> byClass = new();
        private readonly List<RomanFamilyBranch> allBranches = new();
        private int dynamicSequence = 1;

        public void Reset()
        {
            byId.Clear();
            byVariantAndCognomen.Clear();
            byClass.Clear();
            allBranches.Clear();
            dynamicSequence = 1;
        }

        private static string GetVariantKey(RomanGensVariant variant)
        {
            return $"{variant?.Definition?.Id}:{variant?.SocialClass}";
        }

        private static string NormalizeCognomen(string cognomen)
        {
            return RomanNameUtility.Normalize(cognomen);
        }

        public RomanFamilyBranch RegisterBranch(RomanGensVariant variant, string cognomen, bool isDynamic, string parentBranchId = null)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            var normalizedCognomen = NormalizeCognomen(cognomen);
            if (string.IsNullOrEmpty(normalizedCognomen))
                normalizedCognomen = $"Novus{dynamicSequence++}";

            var variantKey = GetVariantKey(variant);
            if (!byVariantAndCognomen.TryGetValue(variantKey, out var cognomenLookup))
            {
                cognomenLookup = new Dictionary<string, RomanFamilyBranch>(StringComparer.OrdinalIgnoreCase);
                byVariantAndCognomen[variantKey] = cognomenLookup;
            }

            if (cognomenLookup.TryGetValue(normalizedCognomen, out var existing))
                return existing;

            string baseId = $"{variant.Definition.Id}-{variant.SocialClass}-{normalizedCognomen}".Replace(' ', '-');
            string id = baseId;
            while (byId.ContainsKey(id))
            {
                id = $"{baseId}-{dynamicSequence++}";
            }

            var branch = new RomanFamilyBranch(id, variant, normalizedCognomen, isDynamic, parentBranchId);
            cognomenLookup[normalizedCognomen] = branch;
            byId[id] = branch;
            allBranches.Add(branch);

            if (!byClass.TryGetValue(branch.SocialClass, out var list))
            {
                list = new List<RomanFamilyBranch>();
                byClass[branch.SocialClass] = list;
            }
            list.Add(branch);

            variant.RegisterCognomen(normalizedCognomen);
            return branch;
        }

        public RomanFamilyBranch CreateDynamicBranch(RomanGensVariant variant, string parentBranchId, Func<RomanGensVariant, string> cognomenFactory)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));
            if (cognomenFactory == null)
                throw new ArgumentNullException(nameof(cognomenFactory));

            string candidate = cognomenFactory(variant);
            return RegisterBranch(variant, candidate, true, parentBranchId);
        }

        public RomanFamilyBranch RehydrateBranch(
            string branchId,
            RomanGensVariant variant,
            string cognomen,
            bool isDynamic,
            string parentBranchId = null)
        {
            if (string.IsNullOrWhiteSpace(branchId))
                throw new ArgumentNullException(nameof(branchId));
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            var normalizedId = branchId.Trim();
            if (byId.TryGetValue(normalizedId, out var existing))
                return existing;

            var normalizedCognomen = NormalizeCognomen(cognomen);
            if (string.IsNullOrEmpty(normalizedCognomen))
                normalizedCognomen = $"Novus{dynamicSequence++}";

            var variantKey = GetVariantKey(variant);
            if (!byVariantAndCognomen.TryGetValue(variantKey, out var cognomenLookup))
            {
                cognomenLookup = new Dictionary<string, RomanFamilyBranch>(StringComparer.OrdinalIgnoreCase);
                byVariantAndCognomen[variantKey] = cognomenLookup;
            }

            if (cognomenLookup.TryGetValue(normalizedCognomen, out var byCognomen))
            {
                byId[normalizedId] = byCognomen;
                return byCognomen;
            }

            var branch = new RomanFamilyBranch(normalizedId, variant, normalizedCognomen, isDynamic, parentBranchId);
            cognomenLookup[normalizedCognomen] = branch;
            byId[normalizedId] = branch;
            allBranches.Add(branch);

            if (!byClass.TryGetValue(branch.SocialClass, out var list))
            {
                list = new List<RomanFamilyBranch>();
                byClass[branch.SocialClass] = list;
            }
            list.Add(branch);

            variant.RegisterCognomen(normalizedCognomen);
            return branch;
        }

        public RomanFamilyBranch GetBranch(string branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
                return null;

            return byId.TryGetValue(branchId, out var branch) ? branch : null;
        }

        public RomanFamilyBranch GetBranch(RomanGensVariant variant, string cognomen)
        {
            if (variant == null)
                return null;

            var variantKey = GetVariantKey(variant);
            if (!byVariantAndCognomen.TryGetValue(variantKey, out var lookup))
                return null;

            var normalized = NormalizeCognomen(cognomen);
            if (string.IsNullOrEmpty(normalized))
                return null;

            return lookup.TryGetValue(normalized, out var branch) ? branch : null;
        }

        public IReadOnlyList<RomanFamilyBranch> GetAllBranches() => allBranches;

        public IReadOnlyList<RomanFamilyBranch> GetBranchesForVariant(RomanGensVariant variant)
        {
            if (variant == null)
                return Array.Empty<RomanFamilyBranch>();

            var variantKey = GetVariantKey(variant);
            if (!byVariantAndCognomen.TryGetValue(variantKey, out var lookup))
                return Array.Empty<RomanFamilyBranch>();

            return lookup.Values.ToList();
        }

        public RomanFamilyBranch GetRandomBranch(SocialClass socialClass, Random random)
        {
            if (!byClass.TryGetValue(socialClass, out var list) || list.Count == 0)
                return null;

            return list[random.Next(list.Count)];
        }
    }
}
