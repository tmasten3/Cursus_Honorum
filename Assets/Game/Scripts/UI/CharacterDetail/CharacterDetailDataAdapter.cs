using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Data.Characters;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace Game.UI.CharacterDetail
{
    public sealed class CharacterDetailDataAdapter
    {
        private readonly CharacterSystem characterSystem;
        private readonly CharacterRepository repository;
        private readonly OfficeSystem officeSystem;
        private readonly ElectionSystem electionSystem;
        private readonly TimeSystem timeSystem;

        private readonly StringBuilder builder = new();
        private readonly List<Character> reusableCharacters = new();

        public CharacterDetailDataAdapter(
            CharacterSystem characterSystem,
            OfficeSystem officeSystem,
            ElectionSystem electionSystem,
            TimeSystem timeSystem)
        {
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            this.officeSystem = officeSystem;
            this.electionSystem = electionSystem;
            this.timeSystem = timeSystem;

            if (!characterSystem.TryGetRepository(out repository))
                repository = null;
        }

        public bool TryBuildSnapshot(int characterId, out CharacterDetailSnapshot snapshot)
        {
            snapshot = default;

            var character = characterSystem.Get(characterId);
            if (character == null)
                return false;

            var identity = BuildIdentitySummary(character);
            var family = BuildFamilySummary(character);
            var offices = BuildOfficeSummary(character);
            var traits = BuildTraitSummary(character);
            var elections = BuildElectionSummary(character);

            snapshot = new CharacterDetailSnapshot(
                character.ID,
                ResolveFullName(character),
                character.Gender,
                character.Class,
                character.IsAlive,
                character.Age,
                character.BirthYear,
                character.BirthMonth,
                character.BirthDay,
                character.Family,
                identity,
                family,
                offices,
                traits,
                elections);

            return true;
        }

        private string BuildIdentitySummary(Character character)
        {
            builder.Clear();
            builder.AppendLine($"ID: {character.ID}");
            builder.AppendLine($"Status: {(character.IsAlive ? "Alive" : "Deceased")} at age {Math.Max(0, character.Age)}");

            if (character.BirthYear != 0 || character.BirthMonth != 0 || character.BirthDay != 0)
            {
                builder.AppendLine($"Birth: {FormatFullDate(character.BirthYear, character.BirthMonth, character.BirthDay)}");
            }

            builder.AppendLine($"Gender: {character.Gender}");
            builder.AppendLine($"Social Class: {character.Class}");

            if (!string.IsNullOrWhiteSpace(character.Family))
                builder.AppendLine($"Family: {character.Family}");

            builder.AppendLine($"Wealth: {character.Wealth}");
            builder.AppendLine($"Influence: {character.Influence}");

            if (character.Ambition != null)
            {
                var ambition = character.Ambition;
                var goal = string.IsNullOrWhiteSpace(ambition.CurrentGoal) ? "Undeclared" : ambition.CurrentGoal;
                builder.Append($"Ambition: {goal} (Intensity {ambition.Intensity}");
                if (ambition.TargetYear.HasValue)
                {
                    builder.Append($", Target {FormatYear(ambition.TargetYear.Value)}");
                }

                builder.Append(ambition.IsRetired ? ", Retired" : string.Empty);
                builder.AppendLine(")");

                if (ambition.History != null && ambition.History.Count > 0)
                {
                    builder.AppendLine("Ambition History:");
                    foreach (var entry in ambition.History
                                 .Where(h => h != null)
                                 .OrderByDescending(h => h.Year))
                    {
                        builder.Append(" • ");
                        builder.Append(FormatYear(entry.Year));
                        if (!string.IsNullOrWhiteSpace(entry.Description))
                        {
                            builder.Append(" - ");
                            builder.Append(entry.Description);
                        }

                        if (!string.IsNullOrWhiteSpace(entry.Outcome))
                        {
                            builder.Append(" (" + entry.Outcome + ")");
                        }

                        builder.AppendLine();
                    }
                }
            }

            if (character.CareerMilestones != null && character.CareerMilestones.Count > 0)
            {
                builder.AppendLine("Career Highlights:");
                foreach (var milestone in character.CareerMilestones
                             .Where(m => m != null)
                             .OrderByDescending(m => m.Year))
                {
                    builder.Append(" • ");
                    builder.Append(FormatYear(milestone.Year));
                    if (!string.IsNullOrWhiteSpace(milestone.Title))
                    {
                        builder.Append(" - ");
                        builder.Append(milestone.Title);
                    }

                    if (!string.IsNullOrWhiteSpace(milestone.Notes))
                    {
                        builder.Append(" (" + milestone.Notes + ")");
                    }

                    builder.AppendLine();
                }
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildFamilySummary(Character character)
        {
            builder.Clear();

            builder.AppendLine("Parents:");
            builder.AppendLine(" • Father: " + FormatRelative(character.FatherID));
            builder.AppendLine(" • Mother: " + FormatRelative(character.MotherID));

            builder.AppendLine("Spouse: " + FormatRelative(character.SpouseID));

            var children = GetChildren(character.ID);
            if (children.Count > 0)
            {
                builder.AppendLine("Children:");
                foreach (var child in children)
                {
                    builder.AppendLine(" • " + FormatRelative(child));
                }
            }
            else
            {
                builder.AppendLine("Children: None recorded.");
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildOfficeSummary(Character character)
        {
            builder.Clear();
            bool hasContent = false;

            if (officeSystem?.StateService != null)
            {
                var activeRecords = officeSystem.StateService.GetActiveRecords(character.ID)?.ToList() ?? new List<ActiveOfficeRecord>();
                if (activeRecords.Count > 0)
                {
                    hasContent = true;
                    builder.AppendLine("Active Offices:");
                    foreach (var record in activeRecords)
                    {
                        builder.Append(" • ");
                        builder.Append(GetOfficeName(record.OfficeId));
                        builder.Append($" (Seat {record.SeatIndex + 1})");
                        if (record.EndYear > 0)
                        {
                            builder.Append($" — Term ends {FormatYear(record.EndYear)}");
                        }

                        builder.AppendLine();
                    }
                }

                var pendingRecords = officeSystem.StateService.GetPendingRecords(character.ID)?.ToList() ?? new List<PendingOfficeRecord>();
                if (pendingRecords.Count > 0)
                {
                    hasContent = true;
                    builder.AppendLine("Pending Assignments:");
                    foreach (var record in pendingRecords)
                    {
                        builder.Append(" • ");
                        builder.Append(GetOfficeName(record.OfficeId));
                        builder.Append($" (Seat {record.SeatIndex + 1}) — Begins {FormatYear(record.StartYear)}");
                        builder.AppendLine();
                    }
                }

                var history = officeSystem.GetCareerHistory(character.ID);
                if (history != null && history.Count > 0)
                {
                    hasContent = true;
                    builder.AppendLine("Career History:");
                    foreach (var record in history.OrderByDescending(r => r?.StartYear ?? int.MinValue))
                    {
                        if (record == null)
                            continue;

                        builder.Append(" • ");
                        builder.Append(GetOfficeName(record.OfficeId));
                        builder.Append($" (Seat {record.SeatIndex + 1}) — {FormatTermRange(record.StartYear, record.EndYear)}");
                        builder.AppendLine();
                    }
                }
            }

            if (!hasContent)
                builder.Append("No office history recorded.");

            return builder.ToString().TrimEnd();
        }

        private string BuildTraitSummary(Character character)
        {
            builder.Clear();
            bool hasContent = false;

            if (character.TraitRecords != null && character.TraitRecords.Count > 0)
            {
                hasContent = true;
                builder.AppendLine("Trait Progress:");
                foreach (var record in character.TraitRecords
                             .Where(r => r != null)
                             .OrderByDescending(r => r.Level))
                {
                    builder.Append(" • ");
                    builder.Append(FormatTraitName(record.Id));
                    builder.Append($" — Level {record.Level}");
                    if (record.AcquiredYear != 0)
                        builder.Append($", since {FormatYear(record.AcquiredYear)}");
                    if (record.Experience > 0f)
                        builder.Append($", XP {record.Experience:F1}");
                    builder.AppendLine();
                }
            }
            else if (character.Traits != null && character.Traits.Count > 0)
            {
                hasContent = true;
                builder.AppendLine("Traits:");
                foreach (var trait in character.Traits.Where(t => !string.IsNullOrWhiteSpace(t)).OrderBy(t => t))
                {
                    builder.AppendLine(" • " + FormatTraitName(trait));
                }
            }

            hasContent = AppendAttributeLine(builder, "Influence", character.Influence) || hasContent;
            hasContent = AppendAttributeLine(builder, "Wealth", character.Wealth) || hasContent;

            if (!hasContent)
                builder.Append("No notable traits or attributes recorded.");

            return builder.ToString().TrimEnd();
        }

        private string BuildElectionSummary(Character character)
        {
            builder.Clear();

            if (electionSystem == null)
            {
                builder.Append("Election data unavailable.");
                return builder.ToString();
            }

            var results = CollectElectionHistory(character.ID);
            if (results.Count == 0)
            {
                builder.Append("No election history recorded.");
                return builder.ToString();
            }

            foreach (var entry in results)
            {
                builder.Append(" • ");
                builder.Append(entry);
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private List<string> CollectElectionHistory(int characterId)
        {
            var lines = new List<string>();

            var (currentYear, _, _) = timeSystem != null ? timeSystem.GetCurrentDate() : (0, 0, 0);
            if (currentYear == 0)
            {
                var reference = characterSystem.Get(characterId);
                if (reference != null)
                    currentYear = reference.BirthYear + reference.Age;
            }

            int startYear = currentYear != 0 ? currentYear : 0;
            int endYear = startYear - 40;

            for (int year = startYear; year >= endYear; year--)
            {
                var records = electionSystem.GetResultsForYear(year);
                if (records == null || records.Count == 0)
                    continue;

                foreach (var record in records)
                {
                    if (record?.Candidates == null)
                        continue;

                    var candidate = record.Candidates.FirstOrDefault(c => c?.Character?.ID == characterId);
                    if (candidate == null)
                        continue;

                    var officeName = record.Office?.Name ?? record.Office?.Id ?? "Office";
                    bool won = record.Winners != null && record.Winners.Any(w => w != null && w.CharacterId == characterId);
                    int rank = DetermineCandidateRank(record, characterId);

                    var lineBuilder = new StringBuilder();
                    lineBuilder.Append($"{FormatYear(record.Year)}: {officeName} — ");
                    if (won)
                    {
                        var winner = record.Winners.First(w => w.CharacterId == characterId);
                        lineBuilder.Append($"Won seat {winner.SeatIndex + 1}");
                        if (!string.IsNullOrWhiteSpace(winner.Notes))
                            lineBuilder.Append($" ({winner.Notes})");
                    }
                    else
                    {
                        lineBuilder.Append("Lost");
                        if (rank > 0)
                            lineBuilder.Append($" (final rank {rank})");
                    }

                    lines.Add(lineBuilder.ToString());
                }
            }

            if (lines.Count > 0)
                lines = lines.Distinct().OrderByDescending(ParseYearPrefix).ToList();

            return lines;
        }

        private static int ParseYearPrefix(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return int.MinValue;

            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
                return int.MinValue;

            var yearText = line.Substring(0, colonIndex).Trim();
            var tokens = yearText.Split(' ');
            if (tokens.Length == 2 && tokens[1].Equals("BCE", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(tokens[0], out var value))
                    return -value;
            }

            if (int.TryParse(yearText, out var year))
                return year;

            return int.MinValue;
        }

        private int DetermineCandidateRank(ElectionResultRecord record, int characterId)
        {
            if (record?.Candidates == null)
                return -1;

            var ordered = record.Candidates
                .Where(c => c?.Character != null)
                .OrderByDescending(c => c.FinalScore)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Character.ID == characterId)
                    return i + 1;
            }

            return -1;
        }

        private string FormatRelative(int? id)
        {
            if (!id.HasValue)
                return "Unknown";

            var relative = GetCharacterById(id.Value);
            if (relative == null)
                return $"Unknown (ID {id.Value})";

            return FormatRelative(relative);
        }

        private string FormatRelative(Character relative)
        {
            if (relative == null)
                return "Unknown";

            var status = relative.IsAlive ? "alive" : "deceased";
            return $"{ResolveFullName(relative)} (ID {relative.ID}, {status}, age {Math.Max(0, relative.Age)})";
        }

        private List<Character> GetChildren(int characterId)
        {
            reusableCharacters.Clear();
            if (repository == null)
                return reusableCharacters;

            foreach (var candidate in repository.AllCharacters)
            {
                if (candidate == null)
                    continue;

                if (candidate.FatherID == characterId || candidate.MotherID == characterId)
                    reusableCharacters.Add(candidate);
            }

            reusableCharacters.Sort((a, b) => string.Compare(ResolveFullName(a), ResolveFullName(b), StringComparison.OrdinalIgnoreCase));
            return reusableCharacters;
        }

        private Character GetCharacterById(int id)
        {
            if (repository != null)
            {
                var resolved = repository.Get(id);
                if (resolved != null)
                    return resolved;
            }

            return characterSystem.Get(id);
        }

        private static string ResolveFullName(Character character)
        {
            if (character == null)
                return string.Empty;

            var name = character.FullName;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var generated = RomanNamingRules.GenerateRomanName(character.Gender, character.Family, character.Class);
            return generated?.GetFullName() ?? $"Character {character.ID}";
        }

        private string GetOfficeName(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
                return "Unknown office";

            var definition = officeSystem?.Definitions?.GetDefinition(officeId);
            return definition?.Name ?? officeId;
        }

        private static string FormatTermRange(int startYear, int endYear)
        {
            var start = FormatYear(startYear);
            var end = FormatYear(endYear);

            if (startYear == 0 && endYear == 0)
                return "Term unknown";
            if (startYear == 0)
                return $"Term ending {end}";
            if (endYear == 0)
                return $"Term starting {start}";
            if (startYear == endYear)
                return $"Term {start}";
            return $"{start} – {end}";
        }

        private static bool AppendAttributeLine(StringBuilder target, string label, int value)
        {
            target.AppendLine($"{label}: {value}");
            return true;
        }

        private static string FormatTraitName(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId))
                return "Unknown trait";

            return char.ToUpperInvariant(traitId[0]) + traitId.Substring(1).Replace('_', ' ');
        }

        private static string FormatFullDate(int year, int month, int day)
        {
            var yearText = FormatYear(year);
            if (month <= 0 && day <= 0)
                return yearText;

            string monthText = month > 0 ? month.ToString("00") : "??";
            string dayText = day > 0 ? day.ToString("00") : "??";
            return $"{dayText}/{monthText}/{yearText}";
        }

        private static string FormatYear(int year)
        {
            if (year == 0)
                return "Year unknown";
            if (year < 0)
                return $"{Math.Abs(year)} BCE";
            return $"{year} CE";
        }
    }
}
