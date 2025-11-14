using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Data.Characters
{
    public static class CharacterDataValidator
    {
        private const string LogCategory = "CharacterData";

        public static void Validate(
            IEnumerable<Character> characters,
            string sourceLabel,
            Action<CharacterValidationIssue> issueReporter = null)
        {
            if (characters == null)
                return;

            var seenIds = new HashSet<int>();
            var index = -1;
            foreach (var character in characters)
            {
                index++;
                if (character == null)
                    continue;

                if (!seenIds.Add(character.ID))
                {
                    var message = $"{sourceLabel}: Duplicate character ID {character.ID} detected.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, "ID", message);
                }

                if (!Enum.IsDefined(typeof(Gender), character.Gender))
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has invalid gender value '{character.Gender}'.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, "Gender", message);
                }

                ValidateBirthData(character, sourceLabel, index, issueReporter);
                ValidateRomanName(character, sourceLabel, index, issueReporter);
            }
        }

        private static void ValidateBirthData(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            if (character.BirthMonth is < 1 or > 12)
            {
                var message =
                    $"{sourceLabel}: Character #{character.ID} has invalid birth month '{character.BirthMonth}'.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "BirthMonth", message);
            }

            if (character.BirthDay is < 1 or > 31)
            {
                var message =
                    $"{sourceLabel}: Character #{character.ID} has invalid birth day '{character.BirthDay}'.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "BirthDay", message);
            }
        }

        private static void ValidateRomanName(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            if (character.RomanName == null)
            {
                var message = $"{sourceLabel}: Character #{character.ID} is missing a RomanName definition.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName", message);
                return;
            }

            if (character.RomanName.Gender != character.Gender)
            {
                var message =
                    $"{sourceLabel}: Character #{character.ID} RomanName gender mismatch (data {character.RomanName.Gender} vs character {character.Gender}).";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Gender", message);
            }

            var fullName = character.RomanName.GetFullName();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                var message = $"{sourceLabel}: Character #{character.ID} has an empty name.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName", message);
            }

            if (character.Gender == Gender.Male)
            {
                ValidateMaleName(character, sourceLabel, index, issueReporter);
            }
            else
            {
                ValidateFemaleName(character, sourceLabel, index, issueReporter);
            }

            if (string.IsNullOrWhiteSpace(character.Family))
            {
                var message = $"{sourceLabel}: Character #{character.ID} is missing gens information.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "Family", message);
            }
        }

        private static void ValidateMaleName(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            var name = character.RomanName;
            if (string.IsNullOrWhiteSpace(name.Praenomen) || string.IsNullOrWhiteSpace(name.Nomen) || string.IsNullOrWhiteSpace(name.Cognomen))
            {
                var message = $"{sourceLabel}: Male character #{character.ID} has incomplete name data.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName", message);
            }

            var parts = new[] { name.Praenomen, name.Nomen, name.Cognomen }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToArray();

            if (parts.Length != parts.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                var message = $"{sourceLabel}: Male character #{character.ID} has duplicate name components.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName", message);
            }
        }

        private static void ValidateFemaleName(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            var name = character.RomanName;
            if (!string.IsNullOrWhiteSpace(name.Praenomen))
            {
                var message = $"{sourceLabel}: Female character #{character.ID} should not have a praenomen.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Praenomen", message);
            }

            if (string.IsNullOrWhiteSpace(name.Nomen))
            {
                var message = $"{sourceLabel}: Female character #{character.ID} is missing a nomen.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Nomen", message);
            }
        }

        private static void ReportIssue(
            Action<CharacterValidationIssue> issueReporter,
            int index,
            string field,
            string message)
        {
            issueReporter?.Invoke(new CharacterValidationIssue
            {
                CharacterIndex = index,
                Field = field,
                Message = message
            });
        }
    }
}
