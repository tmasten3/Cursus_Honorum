using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Characters;

namespace Game.Systems.CharacterSystem
{
    internal sealed class CharacterRepository
    {
        private readonly Dictionary<int, Character> characters = new();
        private readonly HashSet<int> alive = new();
        private readonly HashSet<int> dead = new();
        private readonly Dictionary<(int Month, int Day), List<int>> birthdays = new();
        private readonly Dictionary<string, HashSet<int>> byFamily = new(StringComparer.Ordinal);
        private readonly Dictionary<SocialClass, HashSet<int>> byClass = new();

        public int AliveCount => alive.Count;
        public int FamilyCount => byFamily.Count;

        public IEnumerable<Character> AllCharacters => characters.Values;

        public IEnumerable<int> AliveIdSnapshot() => alive.OrderBy(id => id).ToList();

        public IEnumerable<int> DeadIdSnapshot() => dead.OrderBy(id => id).ToList();

        public void Reset()
        {
            characters.Clear();
            alive.Clear();
            dead.Clear();
            birthdays.Clear();
            byFamily.Clear();
            byClass.Clear();
        }

        public void Add(Character character, bool keepDead)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));

            characters[character.ID] = character;

            if (character.IsAlive) alive.Add(character.ID);
            else if (keepDead) dead.Add(character.ID);

            var birthdayKey = (character.BirthMonth, character.BirthDay);
            if (!birthdays.TryGetValue(birthdayKey, out var birthdayList))
            {
                birthdayList = new List<int>();
                birthdays[birthdayKey] = birthdayList;
            }
            birthdayList.Add(character.ID);

            if (!string.IsNullOrEmpty(character.Family))
            {
                if (!byFamily.TryGetValue(character.Family, out var familySet))
                {
                    familySet = new HashSet<int>();
                    byFamily[character.Family] = familySet;
                }
                familySet.Add(character.ID);
            }

            if (!byClass.TryGetValue(character.Class, out var classSet))
            {
                classSet = new HashSet<int>();
                byClass[character.Class] = classSet;
            }
            classSet.Add(character.ID);
        }

        public Character Get(int id) => characters.TryGetValue(id, out var c) ? c : null;

        public IReadOnlyList<Character> GetAllLiving() =>
            alive.OrderBy(id => id).Select(id => characters[id]).ToList();

        public IReadOnlyList<Character> GetByFamily(string gens)
        {
            if (gens == null) return Array.Empty<Character>();
            return byFamily.TryGetValue(gens, out var set)
                ? set.OrderBy(id => id).Select(id => characters[id]).ToList()
                : Array.Empty<Character>();
        }

        public IReadOnlyList<Character> GetByClass(SocialClass socialClass) =>
            byClass.TryGetValue(socialClass, out var set)
                ? set.OrderBy(id => id).Select(id => characters[id]).ToList()
                : Array.Empty<Character>();

        public IEnumerable<int> EnumerateLivingIds() => alive.OrderBy(id => id).ToList();

        public void AgeUpBirthdays(int month, int day)
        {
            if (!birthdays.TryGetValue((month, day), out var todaysIds))
                return;

            foreach (var id in todaysIds.OrderBy(x => x))
            {
                if (alive.Contains(id) && characters.TryGetValue(id, out var character))
                {
                    character.AgeUp();
                }
            }
        }

        public Character MarkDead(int id, bool keepDead)
        {
            if (!characters.TryGetValue(id, out var character))
                return null;

            if (!alive.Remove(id))
                return null;

            character.IsAlive = false;

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
                    if (characters.ContainsKey(id))
                        alive.Add(id);
                }
            }

            dead.Clear();
            if (keepDead && deadIds != null)
            {
                foreach (var id in deadIds)
                {
                    if (characters.ContainsKey(id))
                        dead.Add(id);
                }
            }

            foreach (var pair in characters)
            {
                pair.Value.IsAlive = alive.Contains(pair.Key);
            }
        }
    }
}
