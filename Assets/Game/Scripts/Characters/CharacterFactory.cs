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
    public enum CharacterLoadMode
    {
        Normal,
        Strict
    }

    public struct CharacterValidationIssue
    {
        public int CharacterIndex;
        public string Field;
        public string Message;
    }

    public class CharacterValidationResult
    {
        public bool Success;
        public List<CharacterValidationIssue> Issues;
    }

    public static class CharacterFactory
    {
        private const string LogCategory = "CharacterData";

        private static LogBatch _normalizationBatch =
            new LogBatch("CharacterData", "characters were normalized", "CharacterNormalizationReport.txt");

        public static CharacterValidationResult LastValidationResult { get; private set; } =
            new CharacterValidationResult { Success = true, Issues = new List<CharacterValidationIssue>() };

        private static readonly System.Random rng = new();
        private static int nextID = 1000;

        private static readonly string[] RoutineNormalizationPrefixes =
        {
            "removed cognomen for non-noble female",
            "normalized gens",
            "adjusted cognomen to"
        };

        private const int MinPoliticalStatValue = 0;
        private const int MaxPoliticalStatValue = 20;
        private const int MinSkillValue = 0;
        private const int MaxSkillValue = 20;

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------
        public static List<Character> LoadBaseCharacters(string path, CharacterLoadMode mode = CharacterLoadMode.Normal)
        {
            ResetValidationResult();

            if (mode == CharacterLoadMode.Normal)
            {
                _normalizationBatch =
                    new LogBatch("CharacterData", "characters were normalized", "CharacterNormalizationReport.txt");
            }

            if (!File.Exists(path))
            {
                var message = $"Base character file not found at '{path}'.";
                Game.Core.Logger.Warn(LogCategory, message);
                AddGlobalIssue("File", message);
                return new List<Character>();
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                var message = $"Failed to read base character file '{path}': {ex.Message}";
                Game.Core.Logger.Error(LogCategory, message);
                AddGlobalIssue("File", message);
                return new List<Character>();
            }

            CharacterDataWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<CharacterDataWrapper>(json);
            }
            catch (Exception ex)
            {
                var message = $"Failed to parse base character JSON from '{path}': {ex.Message}";
                Game.Core.Logger.Error(LogCategory, message);
                AddGlobalIssue("File", message);
                return new List<Character>();
            }

            if (wrapper == null || wrapper.Characters == null)
            {
                var message = $"Failed to parse base character JSON from '{path}'.";
                Game.Core.Logger.Error(LogCategory, message);
                AddGlobalIssue("File", message);
                return new List<Character>();
            }

            if (mode == CharacterLoadMode.Strict)
            {
                CollectStrictValidationIssues(wrapper.Characters, path);
                return wrapper.Characters;
            }

            foreach (var character in wrapper.Characters)
            {
                NormalizeCharacter(character, path);
            }

            ValidateCharacters(wrapper.Characters, path);
            UpdateNextID(wrapper.Characters);
            _normalizationBatch.Flush();
            LastValidationResult.Success = true;
            return wrapper.Characters;
        }

        private static void NormalizeCharacter(Character character, string sourcePath)
        {
            if (character == null)
                return;

            var analysis = AnalyzeCharacter(character);

            character.RomanName = analysis.NormalizedName;
            if (character.RomanName != null)
                character.RomanName.Gender = character.Gender;

            var normalizedNomen = character.RomanName?.Nomen ?? "(unknown)";

            if (!string.IsNullOrEmpty(analysis.ResolvedFamily))
                character.Family = analysis.ResolvedFamily;

            EnsureLifecycleState(character, character.BirthYear + character.Age);

            if (character.TraitRecords == null || character.TraitRecords.Count == 0)
            {
                SeedTraitRecordsFromLegacyTraits(character);
            }

            if (character.Ambition == null)
                character.Ambition = AmbitionProfile.CreateDefault(character);
            else if (string.IsNullOrWhiteSpace(character.Ambition.CurrentGoal))
                character.Ambition.CurrentGoal = AmbitionProfile.InferDefaultGoal(character);

            NormalizePoliticalAttributes(character, sourcePath);

            if (analysis.Corrections.Count > 0)
            {
                var infoCorrections = new List<string>();
                var warningCorrections = new List<string>();

                foreach (var correction in analysis.Corrections)
                {
                    if (IsRoutineNormalization(correction))
                        infoCorrections.Add(correction);
                    else
                        warningCorrections.Add(correction);
                }

                if (warningCorrections.Count > 0)
                {
                    var warningDetails = string.Join("; ", warningCorrections);
                    var record = $"{sourcePath}: Character #{character.ID} - {warningDetails}";
                    if (!string.IsNullOrEmpty(normalizedNomen))
                        record += $" (nomen '{normalizedNomen}')";
                    _normalizationBatch.Add(record);
                }

                if (infoCorrections.Count > 0)
                {
                    Game.Core.Logger.Info(LogCategory,
                        $"{sourcePath}: Character #{character.ID} - {string.Join("; ", infoCorrections)}");
                }
            }
        }

        public static void NormalizeDeserializedCharacter(Character character, string sourceLabel = null)
        {
            if (character == null)
                return;

            NormalizePoliticalAttributes(character, sourceLabel);
        }

        private static void NormalizePoliticalAttributes(Character character, string sourceLabel)
        {
            if (character == null)
                return;

            character.SenatorialInfluence = SanitizeInfluence(character, character.SenatorialInfluence,
                nameof(Character.SenatorialInfluence), sourceLabel);
            character.PopularInfluence = SanitizeInfluence(character, character.PopularInfluence,
                nameof(Character.PopularInfluence), sourceLabel);
            character.MilitaryInfluence = SanitizeInfluence(character, character.MilitaryInfluence,
                nameof(Character.MilitaryInfluence), sourceLabel);
            character.FamilyInfluence = SanitizeInfluence(character, character.FamilyInfluence,
                nameof(Character.FamilyInfluence), sourceLabel);

            character.Oratory = SanitizeStat(character, character.Oratory, MinPoliticalStatValue, MaxPoliticalStatValue,
                nameof(Character.Oratory), sourceLabel);
            character.AmbitionScore = SanitizeStat(character, character.AmbitionScore, MinPoliticalStatValue,
                MaxPoliticalStatValue, nameof(Character.AmbitionScore), sourceLabel);
            character.Courage = SanitizeStat(character, character.Courage, MinPoliticalStatValue, MaxPoliticalStatValue,
                nameof(Character.Courage), sourceLabel);
            character.Dignitas = SanitizeStat(character, character.Dignitas, MinPoliticalStatValue, MaxPoliticalStatValue,
                nameof(Character.Dignitas), sourceLabel);

            character.Administration = SanitizeStat(character, character.Administration, MinSkillValue, MaxSkillValue,
                nameof(Character.Administration), sourceLabel);
            character.Judgment = SanitizeStat(character, character.Judgment, MinSkillValue, MaxSkillValue,
                nameof(Character.Judgment), sourceLabel);
            character.Strategy = SanitizeStat(character, character.Strategy, MinSkillValue, MaxSkillValue,
                nameof(Character.Strategy), sourceLabel);
            character.Civic = SanitizeStat(character, character.Civic, MinSkillValue, MaxSkillValue,
                nameof(Character.Civic), sourceLabel);

            if (!Enum.IsDefined(typeof(FactionType), character.Faction))
            {
                LogNormalizationWarning(character,
                    $"Invalid faction value '{character.Faction}' encountered. Defaulting to {FactionType.Neutral}.",
                    sourceLabel);
                character.Faction = FactionType.Neutral;
            }

            EnsurePoliticalProfileReadiness(character, sourceLabel);

            character.CurrentOffice = SanitizeOfficeAssignment(character, character.CurrentOffice, sourceLabel);

            character.OfficeHistory ??= new List<OfficeHistoryEntry>();

            for (int i = character.OfficeHistory.Count - 1; i >= 0; i--)
            {
                var entry = character.OfficeHistory[i];
                if (entry == null)
                {
                    LogNormalizationWarning(character,
                        $"Removed null office history entry at index {i} during normalization.", sourceLabel);
                    character.OfficeHistory.RemoveAt(i);
                    continue;
                }

                var normalizedEntry = SanitizeOfficeHistoryEntry(character, entry, i, sourceLabel);
                character.OfficeHistory[i] = normalizedEntry;
            }
        }

        private static OfficeAssignment SanitizeOfficeAssignment(Character character, OfficeAssignment assignment,
            string sourceLabel)
        {
            assignment.OfficeId = string.IsNullOrWhiteSpace(assignment.OfficeId)
                ? null
                : assignment.OfficeId.Trim();

            if (!string.IsNullOrEmpty(assignment.OfficeId))
            {
                if (assignment.SeatIndex < 0)
                {
                    LogNormalizationWarning(character,
                        $"Seat index {assignment.SeatIndex} for current office '{assignment.OfficeId}' was below zero and will be reset to 0.",
                        sourceLabel);
                    assignment.SeatIndex = 0;
                }

                if (assignment.StartYear < 0)
                {
                    LogNormalizationWarning(character,
                        $"Start year {assignment.StartYear} for current office '{assignment.OfficeId}' was below zero and will be reset to 0.",
                        sourceLabel);
                    assignment.StartYear = 0;
                }
            }
            else
            {
                assignment.SeatIndex = Mathf.Max(0, assignment.SeatIndex);
                assignment.StartYear = Mathf.Max(0, assignment.StartYear);
            }

            return assignment;
        }


        private static OfficeHistoryEntry SanitizeOfficeHistoryEntry(
            Character character,
            OfficeHistoryEntry entry,
            int index,
            string sourceLabel)
        {
            entry.OfficeId = string.IsNullOrWhiteSpace(entry.OfficeId) ? null : entry.OfficeId.Trim();

            if (entry.SeatIndex < 0)
            {
                LogNormalizationWarning(character,
                    $"Office history entry {index} had seat index {entry.SeatIndex} below zero; resetting to 0.",
                    sourceLabel);
                entry.SeatIndex = 0;
            }

            if (entry.StartYear < 0)
            {
                LogNormalizationWarning(character,
                    $"Office history entry {index} had start year {entry.StartYear} below zero; resetting to 0.",
                    sourceLabel);
                entry.StartYear = 0;
            }

            if (entry.EndYear.HasValue && entry.EndYear.Value < entry.StartYear)
            {
                LogNormalizationWarning(character,
                    $"Office history entry {index} had end year {entry.EndYear.Value} earlier than start year {entry.StartYear}; clamping to start year.",
                    sourceLabel);
                entry.EndYear = entry.StartYear;
            }

            return entry;
        }

        private static float SanitizeInfluence(Character character, float value, string fieldName, string sourceLabel)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                LogNormalizationWarning(character,
                    $"{fieldName} value '{value}' was not a finite number and has been reset to 0.", sourceLabel);
                return 0f;
            }

            if (value < 0f)
            {
                LogNormalizationWarning(character,
                    $"{fieldName} value '{value}' was below zero and has been clamped to 0.", sourceLabel);
                return 0f;
            }

            return value;
        }

        private static void EnsurePoliticalProfileReadiness(Character character, string sourceLabel)
        {
            float totalInfluence = character.SenatorialInfluence + character.PopularInfluence +
                                   character.MilitaryInfluence + character.FamilyInfluence;

            if (float.IsNaN(totalInfluence) || float.IsInfinity(totalInfluence) || totalInfluence < 0f)
            {
                LogNormalizationWarning(character,
                    $"Computed total influence '{totalInfluence}' was invalid and has been reset along with individual pools.",
                    sourceLabel);

                character.SenatorialInfluence = Mathf.Max(0f, character.SenatorialInfluence);
                character.PopularInfluence = Mathf.Max(0f, character.PopularInfluence);
                character.MilitaryInfluence = Mathf.Max(0f, character.MilitaryInfluence);
                character.FamilyInfluence = Mathf.Max(0f, character.FamilyInfluence);

                if (float.IsNaN(totalInfluence) || float.IsInfinity(totalInfluence))
                {
                    character.SenatorialInfluence = 0f;
                    character.PopularInfluence = 0f;
                    character.MilitaryInfluence = 0f;
                    character.FamilyInfluence = 0f;
                }
            }
        }

        private static int SanitizeStat(Character character, int value, int min, int max, string fieldName,
            string sourceLabel)
        {
            int clamped = Mathf.Clamp(value, min, max);
            if (clamped != value)
            {
                LogNormalizationWarning(character,
                    $"{fieldName} value '{value}' was outside the allowed range {min}-{max} and has been clamped to {clamped}.",
                    sourceLabel);
            }

            return clamped;
        }

        private static void LogNormalizationWarning(Character character, string message, string sourceLabel)
        {
            string prefix = string.IsNullOrEmpty(sourceLabel)
                ? $"Character #{character?.ID ?? 0}"
                : $"{sourceLabel}: Character #{character?.ID ?? 0}";
            Game.Core.Logger.Warn(LogCategory, $"{prefix} - {message}");
        }

        private static void CollectStrictValidationIssues(List<Character> characters, string sourcePath)
        {
            if (characters == null)
            {
                LastValidationResult.Success = true;
                return;
            }

            var deterministicRandom = new System.Random(0);

            var deduplicationKeys = new HashSet<string>(StringComparer.Ordinal);

            bool TryAddIssue(CharacterValidationIssue issue)
            {
                var field = issue.Field ?? string.Empty;
                var key = $"{issue.CharacterIndex}:{field}";
                if (!deduplicationKeys.Add(key))
                    return false;

                LastValidationResult.Issues.Add(issue);
                return true;
            }

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null)
                    continue;

                var analysis = AnalyzeCharacter(character, deterministicRandom);
                if (analysis.Corrections == null || analysis.Corrections.Count == 0)
                    continue;

                foreach (var correction in analysis.Corrections)
                {
                    TryAddIssue(new CharacterValidationIssue
                    {
                        CharacterIndex = i,
                        Field = DetermineFieldFromCorrection(correction),
                        Message = $"{sourcePath}: Character #{character.ID} - {correction}"
                    });
                }
            }

            CharacterDataValidator.Validate(characters, sourcePath, issue =>
            {
                TryAddIssue(issue);
            });

            foreach (var issue in EnumerateStructuralRelationshipIssues(characters, sourcePath))
            {
                Game.Core.Logger.Warn(LogCategory, issue.Message);
                TryAddIssue(issue);
            }

            LastValidationResult.Success = LastValidationResult.Issues.Count == 0;
        }

        private static void ValidateCharacters(List<Character> characters, string sourcePath)
        {
            CharacterDataValidator.Validate(characters, sourcePath);

            foreach (var issue in EnumerateStructuralRelationshipIssues(characters, sourcePath))
            {
                Game.Core.Logger.Warn(LogCategory, issue.Message);
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

        private class CharacterNormalizationAnalysis
        {
            public RomanName NormalizedName;
            public string ResolvedFamily;
            public List<string> Corrections;
        }

        private static CharacterNormalizationAnalysis AnalyzeCharacter(Character character, System.Random randomOverride = null)
        {
            if (character == null)
            {
                return new CharacterNormalizationAnalysis
                {
                    Corrections = new List<string>()
                };
            }

            var corrections = new List<string>();

            var normalizedName = RomanNamingRules.NormalizeOrGenerateName(
                character.Gender,
                character.Class,
                character.Family,
                character.RomanName,
                corrections,
                randomOverride);

            string resolvedFamily = RomanNamingRules.ResolveFamilyName(character.Family, normalizedName);
            if (!string.IsNullOrEmpty(resolvedFamily)
                && !string.Equals(character.Family, resolvedFamily, StringComparison.Ordinal))
            {
                corrections.Add($"normalized gens to '{resolvedFamily}'");
            }

            return new CharacterNormalizationAnalysis
            {
                NormalizedName = normalizedName,
                ResolvedFamily = resolvedFamily,
                Corrections = corrections
            };
        }

        private static string DetermineFieldFromCorrection(string correction)
        {
            if (string.IsNullOrEmpty(correction))
                return string.Empty;

            if (correction.IndexOf("praenomen", StringComparison.OrdinalIgnoreCase) >= 0)
                return "RomanName.Praenomen";

            if (correction.IndexOf("nomen", StringComparison.OrdinalIgnoreCase) >= 0)
                return "RomanName.Nomen";

            if (correction.IndexOf("cognomen", StringComparison.OrdinalIgnoreCase) >= 0)
                return "RomanName.Cognomen";

            if (correction.IndexOf("gens", StringComparison.OrdinalIgnoreCase) >= 0
                || correction.IndexOf("family", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Family";

            return string.Empty;
        }

        private static void ResetValidationResult()
        {
            LastValidationResult = new CharacterValidationResult
            {
                Success = true,
                Issues = new List<CharacterValidationIssue>()
            };
        }

        private static void AddGlobalIssue(string field, string message)
        {
            if (LastValidationResult?.Issues == null)
                ResetValidationResult();

            LastValidationResult.Success = false;
            LastValidationResult.Issues.Add(new CharacterValidationIssue
            {
                CharacterIndex = -1,
                Field = field,
                Message = message
            });
        }

        private static IEnumerable<CharacterValidationIssue> EnumerateStructuralRelationshipIssues(
            List<Character> characters,
            string sourcePath)
        {
            if (characters == null)
                yield break;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null)
                    continue;

                string displayName = character.RomanName?.GetFullName() ?? "(unknown)";

                if (character.FatherID == character.ID || character.MotherID == character.ID)
                {
                    yield return new CharacterValidationIssue
                    {
                        CharacterIndex = i,
                        Field = "Relationships.Parent",
                        Message = $"{sourcePath}: Character #{character.ID} '{displayName}' is listed as their own parent."
                    };
                }

                if (character.SpouseID == character.ID)
                {
                    yield return new CharacterValidationIssue
                    {
                        CharacterIndex = i,
                        Field = "Relationships.Spouse",
                        Message = $"{sourcePath}: Character #{character.ID} '{displayName}' is married to themselves."
                    };
                }
            }
        }

        // ------------------------------------------------------------------
        // Runtime creation helpers
        // ------------------------------------------------------------------

        public static Character CreateChild(Character father, Character mother, int year, int month, int day)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var socialClass = father?.Class ?? mother?.Class ?? SocialClass.Plebeian;
            var romanName = RomanNamingRules.GenerateChildName(father, mother, gender, socialClass, rng);
            var familySeed = father?.Family ?? mother?.Family ?? romanName?.Nomen;
            var resolvedFamily = RomanNamingRules.ResolveFamilyName(familySeed, romanName) ?? romanName?.Nomen;

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
                Influence = 0,
                Ambition = AmbitionProfile.CreateDefault(),
                TraitRecords = new List<TraitRecord>()
            };

            if (child.RomanName != null)
                child.RomanName.Gender = child.Gender;

            EnsureLifecycleState(child, year);

            return child;
        }

        public static Character GenerateRandomCharacter(string family, SocialClass socialClass)
        {
            var gender = rng.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
            var romanName = RomanNamingRules.GenerateStandaloneName(gender, socialClass, family, rng);
            var resolvedFamily = RomanNamingRules.ResolveFamilyName(family, romanName) ?? romanName?.Nomen;

            if (romanName != null)
                romanName.Gender = gender;

            var character = new Character
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
                Influence = rng.Next(1, 10),
                Ambition = AmbitionProfile.CreateDefault()
            };

            EnsureLifecycleState(character, character.BirthYear + character.Age);

            return character;
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

            EnsureLifecycleState(clone, clone.BirthYear + clone.Age);

            return clone;
        }

        public static int GetNextID() => nextID++;

        public static void EnsureLifecycleState(Character character, int? evaluationYear = null)
        {
            if (character == null)
                return;

            character.TraitRecords ??= new List<TraitRecord>();
            character.CareerMilestones ??= new List<CareerMilestone>();

            if (character.TraitRecords.Count == 0 && character.Traits != null)
            {
                SeedTraitRecordsFromLegacyTraits(character);
            }
            else
            {
                foreach (var record in character.TraitRecords)
                {
                    if (record == null)
                        continue;

                    record.Id = string.IsNullOrWhiteSpace(record.Id) ? null : record.Id.Trim();
                    if (record.Level < 1)
                        record.Level = 1;
                    if (record.AcquiredYear == 0)
                        record.AcquiredYear = character.BirthYear + Math.Max(12, character.Age - 3);
                }
            }

            character.Ambition ??= AmbitionProfile.CreateDefault(character);
            if (string.IsNullOrWhiteSpace(character.Ambition.CurrentGoal))
                character.Ambition.CurrentGoal = AmbitionProfile.InferDefaultGoal(character);

            if (evaluationYear.HasValue)
                character.Ambition.LastEvaluatedYear = evaluationYear.Value;
            else if (character.Ambition.LastEvaluatedYear == 0)
                character.Ambition.LastEvaluatedYear = character.BirthYear + character.Age;

            if (character.Ambition.History == null)
                character.Ambition.History = new List<AmbitionHistoryRecord>();
        }

        private static void SeedTraitRecordsFromLegacyTraits(Character character)
        {
            if (character?.Traits == null)
                return;

            character.TraitRecords = new List<TraitRecord>();
            int acquiredYear = character.BirthYear + Math.Max(12, character.Age - 3);
            foreach (var trait in character.Traits)
            {
                if (string.IsNullOrWhiteSpace(trait))
                    continue;

                character.TraitRecords.Add(new TraitRecord
                {
                    Id = trait.Trim(),
                    Level = 1,
                    Experience = 0f,
                    AcquiredYear = acquiredYear
                });
            }
        }

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
