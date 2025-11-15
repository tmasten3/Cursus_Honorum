using System;
using System.Collections.Generic;

namespace Game.Data.Characters
{
    /// <summary>
    /// Describes a cognomen branch within a gens and social class.
    /// </summary>
    public sealed class RomanFamilyBranch
    {
        private readonly List<string> _children = new List<string>();

        public string Id { get; }
        public string GensKey { get; }
        public string Cognomen { get; }
        public SocialClass SocialClass { get; }
        public string ParentBranchId { get; private set; }
        public bool IsDynamic { get; }

        public RomanFamilyBranch(string id, string gensKey, string cognomen, SocialClass socialClass, string parentBranchId, bool isDynamic)
        {
            Id = id;
            GensKey = gensKey;
            Cognomen = string.IsNullOrWhiteSpace(cognomen) ? null : cognomen;
            SocialClass = socialClass;
            ParentBranchId = parentBranchId;
            IsDynamic = isDynamic;
        }

        internal void RegisterChild(string childBranchId)
        {
            if (string.IsNullOrEmpty(childBranchId))
                return;

            if (!_children.Contains(childBranchId))
                _children.Add(childBranchId);
        }

        internal void EnsureParent(string parentBranchId)
        {
            if (!string.IsNullOrEmpty(ParentBranchId) || string.IsNullOrEmpty(parentBranchId))
                return;

            ParentBranchId = parentBranchId;
        }

        public IReadOnlyList<string> ChildBranchIds => _children;

        public string DisplayName => string.IsNullOrEmpty(Cognomen)
            ? GensKey
            : $"{GensKey} {Cognomen}";
    }

    /// <summary>
    /// Tracks the active set of gens branches created during runtime generation.
    /// </summary>
    public static class RomanFamilyRegistry
    {
        private static readonly Dictionary<string, RomanFamilyBranch> BranchesById = new Dictionary<string, RomanFamilyBranch>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BranchIdByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static int dynamicBranchCounter = 1;

        public static void Reset()
        {
            BranchesById.Clear();
            BranchIdByKey.Clear();
            dynamicBranchCounter = 1;
        }

        public static RomanFamilyBranch RegisterOrGet(string gensKey, SocialClass socialClass, string cognomen, string parentBranchId, bool isDynamic)
        {
            gensKey = RomanNameUtility.Normalize(gensKey);
            cognomen = RomanNameUtility.Normalize(cognomen);
            gensKey ??= "unknown";

            string key = BuildKey(gensKey, socialClass, cognomen);
            if (BranchIdByKey.TryGetValue(key, out var existingId))
            {
                var existing = BranchesById[existingId];
                if (!string.IsNullOrEmpty(parentBranchId))
                {
                    existing.EnsureParent(parentBranchId);
                    if (BranchesById.TryGetValue(parentBranchId, out var parent))
                        parent.RegisterChild(existing.Id);
                }
                return existing;
            }

            string id = GenerateBranchId(gensKey, cognomen, socialClass, isDynamic);
            var branch = new RomanFamilyBranch(id, gensKey, cognomen, socialClass, parentBranchId, isDynamic);
            BranchesById[id] = branch;
            BranchIdByKey[key] = id;

            if (!string.IsNullOrEmpty(parentBranchId) && BranchesById.TryGetValue(parentBranchId, out var parent))
                parent.RegisterChild(id);

            return branch;
        }

        public static RomanFamilyBranch GetBranch(string branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
                return null;

            return BranchesById.TryGetValue(branchId, out var branch) ? branch : null;
        }

        public static IEnumerable<RomanFamilyBranch> GetAllBranches() => BranchesById.Values;

        public static IEnumerable<RomanFamilyBranch> GetBranchesForGens(string gensKey)
        {
            gensKey = RomanNameUtility.Normalize(gensKey);
            foreach (var branch in BranchesById.Values)
            {
                if (string.Equals(branch.GensKey, gensKey, StringComparison.OrdinalIgnoreCase))
                    yield return branch;
            }
        }

        public static void RebuildFromCharacters(IEnumerable<Character> characters)
        {
            Reset();
            if (characters == null)
                return;

            var pendingWithoutId = new List<Character>();

            foreach (var character in characters)
            {
                if (character?.RomanName == null)
                    continue;

                if (string.IsNullOrEmpty(character.BranchId))
                {
                    pendingWithoutId.Add(character);
                    continue;
                }

                RegisterExistingBranch(character);
            }

            if (pendingWithoutId.Count == 0)
                return;

            foreach (var character in pendingWithoutId)
            {
                var branch = RegisterOrGet(character.Family, character.Class, character.RomanName.Cognomen, character.BranchParentId, character.BranchIsDynamic);
                if (branch != null && !string.IsNullOrEmpty(character.BranchParentId) && BranchesById.TryGetValue(character.BranchParentId, out var parent))
                    parent.RegisterChild(branch.Id);
            }
        }

        private static void RegisterExistingBranch(Character character)
        {
            string gensKey = RomanNameUtility.Normalize(character.Family) ?? "unknown";
            string cognomen = RomanNameUtility.Normalize(character.RomanName.Cognomen);
            string key = BuildKey(gensKey, character.Class, cognomen);

            if (!BranchesById.TryGetValue(character.BranchId, out var branch))
            {
                branch = new RomanFamilyBranch(character.BranchId, gensKey, cognomen, character.Class, character.BranchParentId, character.BranchIsDynamic);
                BranchesById[branch.Id] = branch;
            }
            else
            {
                branch.EnsureParent(character.BranchParentId);
            }

            BranchIdByKey[key] = branch.Id;

            if (!string.IsNullOrEmpty(character.BranchParentId) && BranchesById.TryGetValue(character.BranchParentId, out var parent))
                parent.RegisterChild(branch.Id);

            UpdateDynamicBranchCounter(branch.Id);
        }

        private static void UpdateDynamicBranchCounter(string branchId)
        {
            if (string.IsNullOrEmpty(branchId))
                return;

            int index = branchId.LastIndexOf("-dyn", StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index + 4 >= branchId.Length)
                return;

            var suffix = branchId[(index + 4)..];
            if (!int.TryParse(suffix, out var value))
                return;

            if (value >= dynamicBranchCounter)
                dynamicBranchCounter = value + 1;
        }

        private static string BuildKey(string gensKey, SocialClass socialClass, string cognomen)
        {
            gensKey ??= "unknown";
            string normalizedCognomen = string.IsNullOrEmpty(cognomen) ? "(root)" : cognomen;
            return $"{gensKey.ToLowerInvariant()}|{socialClass}|{normalizedCognomen.ToLowerInvariant()}";
        }

        private static string GenerateBranchId(string gensKey, string cognomen, SocialClass socialClass, bool isDynamic)
        {
            string baseId = string.IsNullOrEmpty(cognomen)
                ? $"{gensKey}-{socialClass}".Trim('-')
                : $"{gensKey}-{cognomen}-{socialClass}";

            baseId = baseId.Replace(' ', '-');

            if (!isDynamic && !BranchesById.ContainsKey(baseId))
                return baseId;

            string candidate;
            do
            {
                candidate = $"{baseId}-dyn{dynamicBranchCounter++}";
            }
            while (BranchesById.ContainsKey(candidate));

            return candidate;
        }
    }
}
