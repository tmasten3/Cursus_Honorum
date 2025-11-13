using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Data.Characters
{
    public static class CharacterDataValidator
    {
        private const string LogCategory = "CharacterData";

        public static void Validate(IEnumerable<Character> characters, string sourceLabel)
        {
            if (characters == null)
                return;

            var seenIds = new HashSet<int>();
            foreach (var character in characters)
            {
                if (character == null)
                    continue;

                if (!seenIds.Add(character.ID))
                {
                    Logger.Warn(LogCategory, $"{sourceLabel}: Duplicate character ID {character.ID} detected.");
                }

                if (!Enum.IsDefined(typeof(Gender), character.Gender))
                {
                    Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} has invalid gender value '{character.Gender}'.");
                }

                ValidateBirthData(character, sourceLabel);
                ValidateRomanName(character, sourceLabel);
            }
        }

        private static void ValidateBirthData(Character character, string sourceLabel)
        {
            if (character.BirthMonth is < 1 or > 12)
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} has invalid birth month '{character.BirthMonth}'.");
            }

            if (character.BirthDay is < 1 or > 31)
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} has invalid birth day '{character.BirthDay}'.");
            }
        }

        private static void ValidateRomanName(Character character, string sourceLabel)
        {
            if (character.RomanName == null)
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} is missing a RomanName definition.");
                return;
            }

            if (character.RomanName.Gender != character.Gender)
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} RomanName gender mismatch (data {character.RomanName.Gender} vs character {character.Gender}).");
            }

            var fullName = character.RomanName.GetFullName();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} has an empty name.");
            }

            if (character.Gender == Gender.Male)
            {
                ValidateMaleName(character, sourceLabel);
            }
            else
            {
                ValidateFemaleName(character, sourceLabel);
            }

            if (string.IsNullOrWhiteSpace(character.Family))
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Character #{character.ID} is missing gens information.");
            }
        }

        private static void ValidateMaleName(Character character, string sourceLabel)
        {
            var name = character.RomanName;
            if (string.IsNullOrWhiteSpace(name.Praenomen) || string.IsNullOrWhiteSpace(name.Nomen) || string.IsNullOrWhiteSpace(name.Cognomen))
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Male character #{character.ID} has incomplete name data.");
            }

            var parts = new[] { name.Praenomen, name.Nomen, name.Cognomen }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToArray();

            if (parts.Length != parts.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Male character #{character.ID} has duplicate name components.");
            }
        }

        private static void ValidateFemaleName(Character character, string sourceLabel)
        {
            var name = character.RomanName;
            if (!string.IsNullOrWhiteSpace(name.Praenomen))
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Female character #{character.ID} should not have a praenomen.");
            }

            if (string.IsNullOrWhiteSpace(name.Nomen))
            {
                Logger.Warn(LogCategory, $"{sourceLabel}: Female character #{character.ID} is missing a nomen.");
            }
        }
    }
}
