using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;

namespace Game.Systems.CharacterSystem
{
    public sealed class CharacterRepository
    {
        private readonly Dictionary<int, Character> byId = new Dictionary<int, Character>();
        private readonly Dictionary<string, List<int>> byName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> nameKeyById = new Dictionary<int, string>();
        private readonly HashSet<int> alive = new HashSet<int>();
        private readonly HashSet<int> dead = new HashSet<int>();
        private readonly Dictionary<(int Month, int Day), List<int>> birthdays = new Dictionary<(int Month, int Day), List<int>>();
        private readonly Dictionary<string, HashSet<int>> byFamily = new(StringComparer.Ordinal);
        private readonly Dictionary<SocialClass, HashSet<int>> byClass = new Dictionary<SocialClass, HashSet<int>>();
        private readonly Dictionary<string, HashSet<int>> byCognomen = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<int>> byBranch = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<int>> byLineage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> cognomenKeyById = new();
        private readonly Dictionary<int, string> branchKeyById = new();
        private readonly Dictionary<int, string> lineageKeyById = new();

        public int AliveCount => alive.Count;
        public int FamilyCount => byFamily.Count;

        public IEnumerable<Character> AllCharacters => byId.Values;

        public IReadOnlyDictionary<int, Character> ById => byId;
        public IReadOnlyDictionary<string, List<int>> ByName => byName;

        public IEnumerable<int> AliveIdSnapshot() => alive.OrderBy(id => id).ToList();
        public IEnumerable<int> DeadIdSnapshot() => dead.OrderBy(id => id).ToList();

        public void Reset()
        {
            byId.Clear();
            byName.Clear();
            nameKeyById.Clear();
            alive.Clear();
            dead.Clear();
            birthdays.Clear();
            byFamily.Clear();
            byClass.Clear();
            byCognomen.Clear();
            byBranch.Clear();
            byLineage.Clear();
            cognomenKeyById.Clear();
            branchKeyById.Clear();
            lineageKeyById.Clear();
        }

        public void Add(Character character, bool keepDead)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (byId.TryGetValue(character.ID, out var existing))
            {
                RemoveFromIndexes(existing.ID, existing, removeBirthdays: true);
            }

            byId[character.ID] = character;

            if (character.IsAlive)
            {
                alive.Add(character.ID);
                AddToNameIndex(character);
            }
            else if (keepDead)
            {
                dead.Add(character.ID);
            }

            AddToBirthdayIndex(character);
            AddToFamilyIndex(character);
            AddToClassIndex(character);
            AddToCognomenIndex(character);
            AddToBranchIndex(character);
            AddToLineageIndex(character);
        }

        public Character Get(int id) => byId.TryGetValue(id, out var c) ? c : null;

        public IReadOnlyList<Character> GetAllLiving()
        {
            var result = new List<Character>();
            foreach (var id in alive.OrderBy(id => id))
            {
                if (byId.TryGetValue(id, out var character))
                {
                    result.Add(character);
                }
                else
                {
                    Logger.Warn("Safety", $"[CharacterRepository] Alive character id {id} missing from index.");
                }
            }

            return result;
        }

        public IReadOnlyList<Character> GetByFamily(string gens)
        {
            if (gens == null)
                return Array.Empty<Character>();

            if (!byFamily.TryGetValue(gens, out var set))
                return Array.Empty<Character>();

            var result = new List<Character>();
            foreach (var id in set.OrderBy(id => id))
            {
                if (byId.TryGetValue(id, out var character))
                    result.Add(character);
                else
                    Logger.Warn("Safety", $"[CharacterRepository] Family index stale for id {id} in gens '{gens}'.");
            }

            return result;
        }

        public IReadOnlyList<Character> GetByClass(SocialClass socialClass) =>
            BuildListFromIndex(byClass, socialClass);

        public IReadOnlyList<Character> GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Array.Empty<Character>();

            if (!byName.TryGetValue(name, out var ids))
                return Array.Empty<Character>();

            var result = new List<Character>();
            foreach (var id in ids.OrderBy(id => id))
            {
                if (byId.TryGetValue(id, out var character))
                    result.Add(character);
                else
                    Logger.Warn("Safety", $"[CharacterRepository] Name index stale for '{name}' id {id}.");
            }

            return result;
        }

        public IReadOnlyList<Character> GetByCognomen(string cognomen) => BuildListFromStringIndex(byCognomen, cognomen);

        public IReadOnlyList<Character> GetByBranch(string branchId) => BuildListFromStringIndex(byBranch, branchId);

        public IReadOnlyList<Character> GetByLineage(string lineageKey) => BuildListFromStringIndex(byLineage, lineageKey);

        private IReadOnlyList<Character> BuildListFromIndex(Dictionary<SocialClass, HashSet<int>> index, SocialClass key)
        {
            if (!index.TryGetValue(key, out var set))
                return Array.Empty<Character>();

            var result = new List<Character>();
            foreach (var id in set.OrderBy(id => id))
            {
                if (byId.TryGetValue(id, out var character))
                    result.Add(character);
                else
                    Logger.Warn("Safety", $"[CharacterRepository] Class index stale for {key} id {id}.");
            }

            return result;
        }

        private IReadOnlyList<Character> BuildListFromStringIndex(Dictionary<string, HashSet<int>> index, string key)
        {
            var normalized = RomanNameUtility.Normalize(key);
            if (string.IsNullOrEmpty(normalized))
                return Array.Empty<Character>();

            if (!index.TryGetValue(normalized, out var set) || set.Count == 0)
                return Array.Empty<Character>();

            var result = new List<Character>();
            foreach (var id in set.OrderBy(id => id))
            {
                if (byId.TryGetValue(id, out var character))
                    result.Add(character);
                else
                    Logger.Warn("Safety", $"[CharacterRepository] String index stale for '{normalized}' id {id}.");
            }

            return result;
        }

        public IEnumerable<int> EnumerateLivingIds() => alive.OrderBy(id => id).ToList();

        public void AgeUpBirthdays(int month, int day)
        {
            if (!birthdays.TryGetValue((month, day), out var todaysIds))
                return;

            foreach (var id in todaysIds.OrderBy(x => x))
            {
                if (alive.Contains(id) && byId.TryGetValue(id, out var character))
                {
                    character.AgeUp();
                }
            }
        }

        public Character MarkDead(int id, bool keepDead)
        {
            if (!byId.TryGetValue(id, out var character))
                return null;

            if (!alive.Remove(id))
                return null;

            character.IsAlive = false;
            RemoveFromNameIndex(id);

            if (keepDead)
                dead.Add(id);
            else
                dead.Remove(id);

            return character;
        }

        public void ApplyLifeState(IEnumerable<int> aliveIds, IEnumerable<int> deadIds, bool keepDead)
        {
            alive.Clear();
            if (aliveIds != null)
            {
                foreach (var id in aliveIds)
                {
                    if (byId.ContainsKey(id))
                        alive.Add(id);
                }
            }

            dead.Clear();
            if (keepDead && deadIds != null)
            {
                foreach (var id in deadIds)
                {
                    if (byId.ContainsKey(id))
                        dead.Add(id);
                }
            }

            foreach (var pair in byId)
            {
                pair.Value.IsAlive = alive.Contains(pair.Key);
            }

            RebuildNameIndex();
        }

        private void AddToBirthdayIndex(Character character)
        {
            var key = (character.BirthMonth, character.BirthDay);
            if (!birthdays.TryGetValue(key, out var list))
            {
                list = new List<int>();
                birthdays[key] = list;
            }

            if (!list.Contains(character.ID))
                list.Add(character.ID);
        }

        private void AddToFamilyIndex(Character character)
        {
            if (string.IsNullOrEmpty(character.Family))
                return;

            if (!byFamily.TryGetValue(character.Family, out var set))
            {
                set = new HashSet<int>();
                byFamily[character.Family] = set;
            }

            set.Add(character.ID);
        }

        private void AddToCognomenIndex(Character character)
        {
            var normalized = RomanNameUtility.Normalize(character?.RomanName?.Cognomen);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!byCognomen.TryGetValue(normalized, out var set))
            {
                set = new HashSet<int>();
                byCognomen[normalized] = set;
            }

            set.Add(character.ID);
            cognomenKeyById[character.ID] = normalized;
        }

        private void AddToBranchIndex(Character character)
        {
            var normalized = RomanNameUtility.Normalize(character.BranchId);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!byBranch.TryGetValue(normalized, out var set))
            {
                set = new HashSet<int>();
                byBranch[normalized] = set;
            }

            set.Add(character.ID);
            branchKeyById[character.ID] = normalized;
        }

        private void AddToLineageIndex(Character character)
        {
            var normalized = RomanNameUtility.Normalize(character.LineageKey);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!byLineage.TryGetValue(normalized, out var set))
            {
                set = new HashSet<int>();
                byLineage[normalized] = set;
            }

            set.Add(character.ID);
            lineageKeyById[character.ID] = normalized;
        }

        private void AddToClassIndex(Character character)
        {
            if (!byClass.TryGetValue(character.Class, out var set))
            {
                set = new HashSet<int>();
                byClass[character.Class] = set;
            }

            set.Add(character.ID);
        }

        private void AddToNameIndex(Character character)
        {
            var key = character.RomanName?.GetFullName();
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (!byName.TryGetValue(key, out var list))
            {
                list = new List<int>();
                byName[key] = list;
            }

            if (!list.Contains(character.ID))
                list.Add(character.ID);

            nameKeyById[character.ID] = key;
        }

        private void RemoveFromIndexes(int id, Character character, bool removeBirthdays)
        {
            RemoveFromNameIndex(id);
            RemoveFromFamilyIndex(id, character);
            RemoveFromClassIndex(id, character);
            RemoveFromCognomenIndex(id);
            RemoveFromBranchIndex(id);
            RemoveFromLineageIndex(id);

            if (removeBirthdays)
                RemoveFromBirthdayIndex(id, character);

            alive.Remove(id);
            dead.Remove(id);
        }

        private void RemoveFromBirthdayIndex(int id, Character character)
        {
            var key = (character.BirthMonth, character.BirthDay);
            if (!birthdays.TryGetValue(key, out var list))
                return;

            list.Remove(id);
            if (list.Count == 0)
                birthdays.Remove(key);
        }

        private void RemoveFromFamilyIndex(int id, Character character)
        {
            if (string.IsNullOrEmpty(character.Family))
                return;

            if (!byFamily.TryGetValue(character.Family, out var set))
                return;

            set.Remove(id);
            if (set.Count == 0)
                byFamily.Remove(character.Family);
        }

        private void RemoveFromClassIndex(int id, Character character)
        {
            if (!byClass.TryGetValue(character.Class, out var set))
                return;

            set.Remove(id);
            if (set.Count == 0)
                byClass.Remove(character.Class);
        }

        private void RemoveFromCognomenIndex(int id)
        {
            if (!cognomenKeyById.TryGetValue(id, out var key))
                return;

            if (byCognomen.TryGetValue(key, out var set))
            {
                set.Remove(id);
                if (set.Count == 0)
                    byCognomen.Remove(key);
            }

            cognomenKeyById.Remove(id);
        }

        private void RemoveFromBranchIndex(int id)
        {
            if (!branchKeyById.TryGetValue(id, out var key))
                return;

            if (byBranch.TryGetValue(key, out var set))
            {
                set.Remove(id);
                if (set.Count == 0)
                    byBranch.Remove(key);
            }

            branchKeyById.Remove(id);
        }

        private void RemoveFromLineageIndex(int id)
        {
            if (!lineageKeyById.TryGetValue(id, out var key))
                return;

            if (byLineage.TryGetValue(key, out var set))
            {
                set.Remove(id);
                if (set.Count == 0)
                    byLineage.Remove(key);
            }

            lineageKeyById.Remove(id);
        }

        private void RemoveFromNameIndex(int id)
        {
            if (!nameKeyById.TryGetValue(id, out var key))
                return;

            if (byName.TryGetValue(key, out var list))
            {
                list.Remove(id);
                if (list.Count == 0)
                    byName.Remove(key);
            }

            nameKeyById.Remove(id);
        }

        private void RebuildNameIndex()
        {
            byName.Clear();
            nameKeyById.Clear();
            foreach (var id in alive)
            {
                if (byId.TryGetValue(id, out var character))
                    AddToNameIndex(character);
            }
        }
    }
}
