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

        private static void CollectStrictValidationIssues(List<Character> characters, string sourcePath)
        {
            if (characters == null)
            {
                LastValidationResult.Success = true;
                return;
            }

            var deterministicRandom = new System.Random(0);

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
                    LastValidationResult.Issues.Add(new CharacterValidationIssue
                    {
                        CharacterIndex = i,
                        Field = DetermineFieldFromCorrection(correction),
                        Message = $"{sourcePath}: Character #{character.ID} - {correction}"
                    });
                }
            }

            CharacterDataValidator.Validate(characters, sourcePath, issue =>
            {
                LastValidationResult.Issues.Add(issue);
            });

            LastValidationResult.Success = LastValidationResult.Issues.Count == 0;
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

            return "RomanName";
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
            var canonicalFamily = RomanNamingRules.ResolveFamilyName(family, null);
            var romanName = RomanNamingRules.NormalizeOrGenerateName(gender, socialClass, canonicalFamily, null);
            var resolvedFamily = RomanNamingRules.ResolveFamilyName(canonicalFamily, romanName) ?? canonicalFamily ?? romanName?.Nomen;

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
