using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Data.Characters
{
    public static class CharacterDataValidator
    {
        private const string LogCategory = "CharacterData";
        private const int MinPoliticalStatValue = 0;
        private const int MaxPoliticalStatValue = 20;
        private const int MinSkillValue = 0;
        private const int MaxSkillValue = 20;

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
                ValidatePoliticalData(character, sourceLabel, index, issueReporter);
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
                if (character.Gender == Gender.Male)
                {
                    ReportMissingMaleNameComponents(character, sourceLabel, index, issueReporter);
                }
                else if (character.Gender == Gender.Female)
                {
                    var message = $"{sourceLabel}: Character #{character.ID} is missing a nomen.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, "RomanName.Nomen", message);
                }
                return;
            }

            if (character.RomanName.Gender != character.Gender)
            {
                var message =
                    $"{sourceLabel}: Character #{character.ID} RomanName gender mismatch (data {character.RomanName.Gender} vs character {character.Gender}).";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Gender", message);
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
            if (name == null)
            {
                ReportMissingMaleNameComponents(character, sourceLabel, index, issueReporter);
                return;
            }

            bool hasPraenomen = !string.IsNullOrWhiteSpace(name.Praenomen);
            bool hasNomen = !string.IsNullOrWhiteSpace(name.Nomen);
            bool hasCognomen = !string.IsNullOrWhiteSpace(name.Cognomen);

            if (!hasPraenomen)
            {
                var message = $"{sourceLabel}: Male character #{character.ID} is missing a praenomen.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Praenomen", message);
            }

            if (!hasNomen)
            {
                var message = $"{sourceLabel}: Male character #{character.ID} is missing a nomen.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Nomen", message);
            }

            if (!hasCognomen)
            {
                var message = $"{sourceLabel}: Male character #{character.ID} is missing a cognomen.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, "RomanName.Cognomen", message);
            }

            var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (hasPraenomen)
                parts["RomanName.Praenomen"] = name.Praenomen.Trim();
            if (hasNomen)
                parts["RomanName.Nomen"] = name.Nomen.Trim();
            if (hasCognomen)
                parts["RomanName.Cognomen"] = name.Cognomen.Trim();

            foreach (var group in parts
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() <= 1)
                    continue;

                foreach (var entry in group)
                {
                    var message =
                        $"{sourceLabel}: Male character #{character.ID} has duplicate name component '{entry.Value}'.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, entry.Key, message);
                }
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

        private static void ValidatePoliticalData(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            void CheckInfluence(float value, string field)
            {
                if (value < 0f)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has negative influence value '{value}' for {field}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, field, message);
                }
            }

            CheckInfluence(character.SenatorialInfluence, nameof(Character.SenatorialInfluence));
            CheckInfluence(character.PopularInfluence, nameof(Character.PopularInfluence));
            CheckInfluence(character.MilitaryInfluence, nameof(Character.MilitaryInfluence));
            CheckInfluence(character.FamilyInfluence, nameof(Character.FamilyInfluence));

            void CheckStat(int value, string field, int min, int max)
            {
                if (value < min || value > max)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has {field} value '{value}' outside range {min}-{max}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, field, message);
                }
            }

            CheckStat(character.Oratory, nameof(Character.Oratory), MinPoliticalStatValue, MaxPoliticalStatValue);
            CheckStat(character.AmbitionScore, nameof(Character.AmbitionScore), MinPoliticalStatValue, MaxPoliticalStatValue);
            CheckStat(character.Courage, nameof(Character.Courage), MinPoliticalStatValue, MaxPoliticalStatValue);
            CheckStat(character.Dignitas, nameof(Character.Dignitas), MinPoliticalStatValue, MaxPoliticalStatValue);

            CheckStat(character.Administration, nameof(Character.Administration), MinSkillValue, MaxSkillValue);
            CheckStat(character.Judgment, nameof(Character.Judgment), MinSkillValue, MaxSkillValue);
            CheckStat(character.Strategy, nameof(Character.Strategy), MinSkillValue, MaxSkillValue);
            CheckStat(character.Civic, nameof(Character.Civic), MinSkillValue, MaxSkillValue);

            if (!Enum.IsDefined(typeof(FactionType), character.Faction))
            {
                var message =
                    $"{sourceLabel}: Character #{character.ID} has invalid faction value '{character.Faction}'.";
                Logger.Warn(LogCategory, message);
                ReportIssue(issueReporter, index, nameof(Character.Faction), message);
            }

            if (!string.IsNullOrEmpty(character.CurrentOffice.OfficeId))
            {
                if (character.CurrentOffice.SeatIndex < 0)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has negative seat index {character.CurrentOffice.SeatIndex} for current office '{character.CurrentOffice.OfficeId}'.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, nameof(OfficeAssignment.SeatIndex), message);
                }

                if (character.CurrentOffice.StartYear < 0)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has invalid start year {character.CurrentOffice.StartYear} for current office '{character.CurrentOffice.OfficeId}'.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, nameof(OfficeAssignment.StartYear), message);
                }
            }

            if (character.OfficeHistory == null)
                return;

            for (int i = 0; i < character.OfficeHistory.Count; i++)
            {
                var entry = character.OfficeHistory[i];
                if (entry == null)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has a null office history entry at index {i}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, $"OfficeHistory[{i}]", message);
                    continue;
                }

                if (entry.SeatIndex < 0)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has negative seat index {entry.SeatIndex} in office history entry {i}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, $"OfficeHistory[{i}].SeatIndex", message);
                }

                if (entry.StartYear < 0)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has invalid start year {entry.StartYear} in office history entry {i}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, $"OfficeHistory[{i}].StartYear", message);
                }

                if (entry.EndYear.HasValue && entry.EndYear.Value < entry.StartYear)
                {
                    var message =
                        $"{sourceLabel}: Character #{character.ID} has office history entry {i} with end year {entry.EndYear} earlier than start year {entry.StartYear}.";
                    Logger.Warn(LogCategory, message);
                    ReportIssue(issueReporter, index, $"OfficeHistory[{i}].EndYear", message);
                }
            }
        }

        private static void ReportMissingMaleNameComponents(
            Character character,
            string sourceLabel,
            int index,
            Action<CharacterValidationIssue> issueReporter)
        {
            var messagePraenomen = $"{sourceLabel}: Male character #{character?.ID ?? 0} is missing a praenomen.";
            Logger.Warn(LogCategory, messagePraenomen);
            ReportIssue(issueReporter, index, "RomanName.Praenomen", messagePraenomen);

            var messageNomen = $"{sourceLabel}: Male character #{character?.ID ?? 0} is missing a nomen.";
            Logger.Warn(LogCategory, messageNomen);
            ReportIssue(issueReporter, index, "RomanName.Nomen", messageNomen);

            var messageCognomen = $"{sourceLabel}: Male character #{character?.ID ?? 0} is missing a cognomen.";
            Logger.Warn(LogCategory, messageCognomen);
            ReportIssue(issueReporter, index, "RomanName.Cognomen", messageCognomen);
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
