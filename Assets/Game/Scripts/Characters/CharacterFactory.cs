using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Data.Characters
{
    /// <summary>
    /// Handles character creation, loading, and validation.
    /// Uses structured RomanName generation.
    /// </summary>
    public static class CharacterFactory
    {
        private const string LogCategory = "CharacterData";

        private static readonly System.Random rng = new();
        private static int nextID = 1000;

        private static readonly string[] RoutineNormalizationPrefixes =
        {
            "removed cognomen for non-noble female",
            "normalized gens",
            "adjusted cognomen to"
        };

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------
        public static List<Character> LoadBaseCharacters(string path)
        {
            if (!File.Exists(path))
            {
                Game.Core.Logger.Warn(LogCategory, $"Base character file not found at '{path}'.");
                return new List<Character>();
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Error(LogCategory, $"Failed to read base character file '{path}': {ex.Message}");
                return new List<Character>();
            }

            CharacterDataWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<CharacterDataWrapper>(json);
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Error(LogCategory, $"Failed to parse base character JSON from '{path}': {ex.Message}");
                return new List<Character>();
            }

            if (wrapper == null || wrapper.Characters == null)
            {
                Game.Core.Logger.Error(LogCategory, $"Failed to parse base character JSON from '{path}'.");
                return new List<Character>();
            }

            foreach (var character in wrapper.Characters)
            {
                NormalizeCharacter(character, path);
            }

            ValidateCharacters(wrapper.Characters, path);
            UpdateNextID(wrapper.Characters);
            return wrapper.Characters;
        }

        private static void NormalizeCharacter(Character character, string sourcePath)
        {
            if (character == null)
                return;

            var corrections = new List<string>();

            var normalizedName = RomanNamingRules.NormalizeOrGenerateName(
                character.Gender,
                character.Class,
                character.Family,
                character.RomanName,
                corrections);

            character.RomanName = normalizedName;
            if (character.RomanName != null)
                character.RomanName.Gender = character.Gender;

            string resolvedFamily = RomanNamingRules.ResolveFamilyName(character.Family, character.RomanName);
            if (!string.IsNullOrEmpty(resolvedFamily) && !string.Equals(character.Family, resolvedFamily, StringComparison.Ordinal))
                corrections.Add($"normalized gens to '{resolvedFamily}'");

            if (!string.IsNullOrEmpty(resolvedFamily))
                character.Family = resolvedFamily;

            if (corrections.Count > 0)
            {
                var infoCorrections = new List<string>();
                var warningCorrections = new List<string>();

                foreach (var correction in corrections)
                {
                    if (IsRoutineNormalization(correction))
                        infoCorrections.Add(correction);
                    else
                        warningCorrections.Add(correction);
                }

                if (warningCorrections.Count > 0)
                {
                    Game.Core.Logger.Warn(LogCategory,
                        $"{sourcePath}: Character #{character.ID} - {string.Join("; ", warningCorrections)}");
                }

                if (infoCorrections.Count > 0)
                {
                    Game.Core.Logger.Info(LogCategory,
                        $"{sourcePath}: Character #{character.ID} - {string.Join("; ", infoCorrections)}");
                }
            }
        }

        private static void ValidateCharacters(List<Character> characters, string sourcePath)
        {
            CharacterDataValidator.Validate(characters, sourcePath);

            foreach (var c in characters)
            {
                if (c == null)
                    continue;

                if (c.FatherID == c.ID || c.MotherID == c.ID)
                {
                    Game.Core.Logger.Warn(LogCategory, $"{sourcePath}: Character #{c.ID} '{c.RomanName?.GetFullName()}' is listed as their own parent.");
                }

                if (c.SpouseID == c.ID)
                {
                    Game.Core.Logger.Warn(LogCategory, $"{sourcePath}: Character #{c.ID} '{c.RomanName?.GetFullName()}' is married to themselves.");
                }
            }
        }

        private static void UpdateNextID(List<Character> chars)
        {
            foreach (var c in chars)
            {
                if (c != null && c.ID >= nextID)
                    nextID = c.ID + 1;
            }
        }

        // ------------------------------------------------------------------
        // Runtime creation helpers
        // ------------------------------------------------------------------

        public static Character CreateChild(Character father, Character mother, int year, int month, int day)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var socialClass = father?.Class ?? mother?.Class ?? SocialClass.Plebeian;
            var familySeed = father?.Family ?? mother?.Family;
            var canonicalFamily = RomanNamingRules.ResolveFamilyName(familySeed, null);

            var romanName = RomanNamingRules.NormalizeOrGenerateName(gender, socialClass, canonicalFamily, null);
            var resolvedFamily = RomanNamingRules.ResolveFamilyName(canonicalFamily, romanName) ?? canonicalFamily ?? romanName?.Nomen;

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
                Family = resolvedFamily,
                Class = socialClass,
                Wealth = 0,
                Influence = 0
            };

            if (child.RomanName != null)
                child.RomanName.Gender = child.Gender;

            return child;
        }

        public static Character GenerateRandomCharacter(string family, SocialClass socialClass)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var canonicalFamily = RomanNamingRules.ResolveFamilyName(family, null);
            var romanName = RomanNamingRules.NormalizeOrGenerateName(gender, socialClass, canonicalFamily, null);
            var resolvedFamily = RomanNamingRules.ResolveFamilyName(canonicalFamily, romanName) ?? canonicalFamily ?? romanName?.Nomen;

            if (romanName != null)
                romanName.Gender = gender;

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
                Family = resolvedFamily,
                Class = socialClass,
                Wealth = rng.Next(500, 5000),
                Influence = rng.Next(1, 10)
            };
        }

        public static Character CreateFromTemplate(Character template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var clone = template.Clone();
            clone.ID = GetNextID();

            if (clone.RomanName != null)
                clone.RomanName.Gender = clone.Gender;

            var normalizedFamily = RomanNamingRules.ResolveFamilyName(clone.Family, clone.RomanName);
            if (!string.IsNullOrEmpty(normalizedFamily))
                clone.Family = normalizedFamily;

            return clone;
        }

        public static int GetNextID() => nextID++;

        private static bool IsRoutineNormalization(string correction)
        {
            if (string.IsNullOrWhiteSpace(correction))
                return false;

            foreach (var prefix in RoutineNormalizationPrefixes)
            {
                if (correction.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
