using System;
using System.Collections.Generic;

namespace Game.Data.Characters.Generation
{
    internal sealed class PopulationIndex
    {
        public Dictionary<string, List<int>> ByGens { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> ByCognomen { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> ByBranch { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> ByLineage { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Register(Character character)
        {
            if (character == null)
                return;

            AddToIndex(ByGens, character.Family, character.ID);
            AddToIndex(ByCognomen, character.RomanName?.Cognomen, character.ID);
            AddToIndex(ByBranch, character.BranchId, character.ID);
            AddToIndex(ByLineage, character.LineageKey, character.ID);
        }

        private static void AddToIndex(Dictionary<string, List<int>> index, string key, int value)
        {
            var normalized = RomanNameUtility.Normalize(key);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!index.TryGetValue(normalized, out var list))
            {
                list = new List<int>();
                index[normalized] = list;
            }

            list.Add(value);
        }
    }
}
