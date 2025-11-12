using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Handles character creation, loading, and validation.
    /// Uses structured RomanName generation.
    /// </summary>
    public static class CharacterFactory
    {
        private static readonly System.Random rng = new();
        private static int nextID = 1000;

        // ----------------------------------------------------------------------
        // 🔹 Load Base Characters
        // ----------------------------------------------------------------------
        public static List<Character> LoadBaseCharacters(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CharacterFactory] No character file found at {path}");
                return new List<Character>();
            }

            string json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<CharacterDataWrapper>(json);
            if (wrapper == null || wrapper.Characters == null)
            {
                Debug.LogError("[CharacterFactory] Failed to parse base character JSON.");
                return new List<Character>();
            }

            // ensure every character has a RomanName structure
            foreach (var c in wrapper.Characters)
            {
                if (c.RomanName == null)
                {
                    c.RomanName = RomanNamingRules.GenerateRomanName(c.Gender, c.Family, c.Class);
                }
            }

            ValidateCharacters(wrapper.Characters);
            UpdateNextID(wrapper.Characters);
            return wrapper.Characters;
        }

        // ----------------------------------------------------------------------
        // 🔹 Validation
        // ----------------------------------------------------------------------
        private static void ValidateCharacters(List<Character> chars)
        {
            var seenIDs = new HashSet<int>();

            foreach (var c in chars)
            {
                if (seenIDs.Contains(c.ID))
                    Debug.LogWarning($"[CharacterFactory] Duplicate character ID {c.ID}: {c.FullName}");
                else
                    seenIDs.Add(c.ID);

                if (c.FatherID == c.ID || c.MotherID == c.ID)
                    Debug.LogWarning($"[CharacterFactory] {c.FullName} is their own parent!");

                if (c.SpouseID == c.ID)
                    Debug.LogWarning($"[CharacterFactory] {c.FullName} is married to themselves!");
            }
        }

        private static void UpdateNextID(List<Character> chars)
        {
            foreach (var c in chars)
                if (c.ID >= nextID)
                    nextID = c.ID + 1;
        }

        // ----------------------------------------------------------------------
        // 🔹 Runtime Creation
        // ----------------------------------------------------------------------

        /// <summary>
        /// Creates a new child with inherited family and social class.
        /// </summary>
        public static Character CreateChild(Character father, Character mother, int year, int month, int day)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var gens = father?.Family ?? mother?.Family ?? RomanNamingRules.GetNomen(SocialClass.Plebeian);
            var socialClass = father?.Class ?? mother?.Class ?? SocialClass.Plebeian;

            var romanName = RomanNamingRules.GenerateRomanName(gender, gens, socialClass);

            var child = new Character
            {
                ID = GetNextID(),
                RomanName = romanName,
                Gender = gender,
                BirthYear = year,
                BirthMonth = month,
                BirthDay = day,
                Age = 0,
                IsAlive = true,
                FatherID = father?.ID,
                MotherID = mother?.ID,
                Family = gens,
                Class = socialClass,
                Wealth = 0,
                Influence = 0
            };

            Debug.Log($"[CharacterFactory] New child born: {romanName.GetFullName()} ({gender})");
            return child;
        }

        /// <summary>
        /// Generates a random adult for initialization or testing.
        /// </summary>
        public static Character GenerateRandomCharacter(string family, SocialClass socialClass)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var romanName = RomanNamingRules.GenerateRomanName(gender, family, socialClass);

            return new Character
            {
                ID = GetNextID(),
                RomanName = romanName,
                Gender = gender,
                BirthYear = -248,
                BirthMonth = 1,
                BirthDay = 1,
                Age = rng.Next(16, 45),
                IsAlive = true,
                Family = romanName.Nomen,
                Class = socialClass,
                Wealth = rng.Next(500, 5000),
                Influence = rng.Next(1, 10)
            };
        }

        /// <summary>
        /// Creates a deep copy of an existing character as a new instance.
        /// </summary>
        public static Character CreateFromTemplate(Character template)
        {
            var clone = template.Clone();
            clone.ID = GetNextID();
            return clone;
        }

        // ----------------------------------------------------------------------
        // 🔹 Utility
        // ----------------------------------------------------------------------
        public static int GetNextID() => nextID++;
    }
}
