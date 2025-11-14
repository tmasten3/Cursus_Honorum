using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using UnityEngine;

namespace Game.UI
{
    public sealed class DebugOverlayDataProvider
    {
        public readonly struct DebugOverlaySnapshot
        {
            public DebugOverlaySnapshot(
                string date,
                string living,
                string families,
                string officeHeader,
                string officeBody,
                string electionHeader,
                string electionBody,
                string logText,
                int logCount,
                DateTime latestLogTimestamp,
                int officeHash,
                int electionHash,
                float ticksPerSecond)
            {
                Date = date;
                CharacterCountText = living;
                FamilyCountText = families;
                OfficeHeader = officeHeader;
                OfficeBody = officeBody;
                ElectionHeader = electionHeader;
                ElectionBody = electionBody;
                LogText = logText;
                LogCount = logCount;
                LatestLogTimestamp = latestLogTimestamp;
                OfficeDisplayHash = officeHash;
                ElectionDisplayHash = electionHash;
                TicksPerSecond = ticksPerSecond;
            }

            public string Date { get; }
            public string CharacterCountText { get; }
            public string FamilyCountText { get; }
            public string OfficeHeader { get; }
            public string OfficeBody { get; }
            public string ElectionHeader { get; }
            public string ElectionBody { get; }
            public string LogText { get; }
            public int LogCount { get; }
            public DateTime LatestLogTimestamp { get; }
            public int OfficeDisplayHash { get; }
            public int ElectionDisplayHash { get; }
            public float TicksPerSecond { get; }
        }

        public readonly struct DebugOverlayConfiguration
        {
            public DebugOverlayConfiguration(int maxOfficeEntries, int maxElectionEntries, int maxWinnersPerElection, int electionLookbackYears)
            {
                MaxOfficeEntries = maxOfficeEntries;
                MaxElectionEntries = maxElectionEntries;
                MaxWinnersPerElection = maxWinnersPerElection;
                ElectionLookbackYears = electionLookbackYears;
            }

            public int MaxOfficeEntries { get; }
            public int MaxElectionEntries { get; }
            public int MaxWinnersPerElection { get; }
            public int ElectionLookbackYears { get; }
        }

        private readonly TimeSystem timeSystem;
        private readonly CharacterSystem characterSystem;
        private readonly CharacterRepository characterRepository;
        private readonly OfficeSystem officeSystem;
        private readonly ElectionSystem electionSystem;
        private readonly StringBuilder builder = new();

        public DebugOverlayDataProvider(
            TimeSystem timeSystem,
            CharacterSystem characterSystem,
            CharacterRepository characterRepository,
            OfficeSystem officeSystem,
            ElectionSystem electionSystem)
        {
            this.timeSystem = timeSystem;
            this.characterSystem = characterSystem;
            this.characterRepository = characterRepository;
            this.officeSystem = officeSystem;
            this.electionSystem = electionSystem;
        }

        public DebugOverlaySnapshot CreateSnapshot(DebugOverlayConfiguration configuration)
        {
            string dateText = BuildDateText();
            string livingText = BuildLivingText();
            string familyText = BuildFamilyText();

            var officeSection = BuildOfficeSection(configuration.MaxOfficeEntries);
            var electionSection = BuildElectionSection(configuration.MaxElectionEntries, configuration.MaxWinnersPerElection, configuration.ElectionLookbackYears);
            var logsSection = BuildLogsSection();

            float ticksPerSecond = CalculateTicksPerSecond();

            return new DebugOverlaySnapshot(
                dateText,
                livingText,
                familyText,
                officeSection.Header,
                officeSection.Body,
                electionSection.Header,
                electionSection.Body,
                logsSection.Text,
                logsSection.Count,
                logsSection.LatestTimestamp,
                officeSection.Hash,
                electionSection.Hash,
                ticksPerSecond);
        }

        private string BuildDateText()
        {
            string dateString = timeSystem != null ? timeSystem.GetCurrentDateString() : "Date unavailable";
            return $"Date: {dateString}";
        }

        private string BuildLivingText()
        {
            int living = characterSystem?.CountAlive() ?? 0;
            return $"Living: {living}";
        }

        private string BuildFamilyText()
        {
            int families = characterRepository?.FamilyCount ?? characterSystem?.GetFamilyCount() ?? 0;
            return $"Families: {families}";
        }

        private (string Header, string Body, int Hash) BuildOfficeSection(int maxEntries)
        {
            if (officeSystem == null || characterSystem == null)
            {
                return ("Offices: unavailable", string.Empty, 0);
            }

            var definitions = officeSystem.GetAllDefinitions();
            var living = characterSystem.GetAllLiving();
            if (definitions == null || definitions.Count == 0 || living == null || living.Count == 0)
            {
                const string header = "Offices: none";
                const string body = "No active office holders.";
                return (header, body, body.GetHashCode());
            }

            var definitionsById = definitions
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);

            var rows = new List<OfficeDisplayRow>();
            for (int i = 0; i < living.Count; i++)
            {
                var character = living[i];
                if (character == null)
                    continue;

                var holdings = officeSystem.GetCurrentHoldings(character.ID);
                if (holdings == null || holdings.Count == 0)
                    continue;

                for (int j = 0; j < holdings.Count; j++)
                {
                    var seat = holdings[j];
                    if (seat == null || !seat.HolderId.HasValue)
                        continue;

                    var holderName = !string.IsNullOrEmpty(character.FullName) ? character.FullName : $"#{seat.HolderId.Value}";
                    definitionsById.TryGetValue(seat.OfficeId ?? string.Empty, out var definition);

                    string pendingName = null;
                    if (seat.PendingHolderId.HasValue)
                    {
                        var pending = characterSystem.Get(seat.PendingHolderId.Value);
                        pendingName = !string.IsNullOrEmpty(pending?.FullName)
                            ? pending.FullName
                            : $"#{seat.PendingHolderId.Value}";
                    }

                    rows.Add(new OfficeDisplayRow
                    {
                        OfficeId = seat.OfficeId,
                        OfficeName = definition?.Name ?? seat.OfficeId ?? "Office",
                        Rank = definition?.Rank ?? int.MinValue,
                        SeatIndex = seat.SeatIndex,
                        HolderName = holderName,
                        StartYear = seat.StartYear,
                        EndYear = seat.EndYear,
                        PendingName = pendingName,
                        PendingStartYear = seat.PendingStartYear
                    });
                }
            }

            if (rows.Count == 0)
            {
                const string header = "Offices: none";
                const string body = "No active office holders.";
                return (header, body, body.GetHashCode());
            }

            rows.Sort(CompareOfficeRows);

            int total = rows.Count;
            int displayCount = Mathf.Clamp(maxEntries, 1, total);

            builder.Clear();
            for (int i = 0; i < displayCount; i++)
            {
                var row = rows[i];
                builder.Append(row.OfficeName);
                if (row.SeatIndex >= 0)
                {
                    builder.Append(" #").Append(row.SeatIndex + 1);
                }

                builder.Append(':').Append(' ').Append(row.HolderName);

                if (row.StartYear != 0 || row.EndYear != 0)
                {
                    builder.Append(" (");
                    if (row.StartYear == row.EndYear)
                        builder.Append(row.StartYear);
                    else
                        builder.Append(row.StartYear).Append('-').Append(row.EndYear);
                    builder.Append(')');
                }

                if (!string.IsNullOrEmpty(row.PendingName))
                {
                    builder.Append(" → ").Append(row.PendingName);
                    if (row.PendingStartYear > 0)
                        builder.Append(" (from ").Append(row.PendingStartYear).Append(')');
                }

                if (i < displayCount - 1)
                    builder.Append('\n');
            }

            if (total > displayCount)
            {
                builder.Append('\n').Append('+').Append(total - displayCount).Append(" more…");
            }

            string text = builder.ToString();
            builder.Clear();

            string headerText = total > displayCount
                ? $"Offices ({displayCount}/{total})"
                : $"Offices ({total})";

            return (headerText, text, text.GetHashCode());
        }

        private static int CompareOfficeRows(OfficeDisplayRow a, OfficeDisplayRow b)
        {
            int rankCompare = b.Rank.CompareTo(a.Rank);
            if (rankCompare != 0)
                return rankCompare;

            int nameCompare = string.Compare(a.OfficeName, b.OfficeName, StringComparison.OrdinalIgnoreCase);
            if (nameCompare != 0)
                return nameCompare;

            int seatCompare = a.SeatIndex.CompareTo(b.SeatIndex);
            if (seatCompare != 0)
                return seatCompare;

            return string.Compare(a.HolderName, b.HolderName, StringComparison.OrdinalIgnoreCase);
        }

        private (string Header, string Body, int Hash) BuildElectionSection(int maxEntries, int maxWinnersPerElection, int lookbackYears)
        {
            if (electionSystem == null || timeSystem == null)
            {
                return ("Recent Elections: unavailable", string.Empty, 0);
            }

            var currentDate = timeSystem.GetCurrentDate();
            int year = currentDate.year;
            if (year == 0)
            {
                const string header = "Recent Elections";
                const string body = "No election timeline available.";
                return (header, body, body.GetHashCode());
            }

            var entries = GatherRecentElectionRows(year, Mathf.Max(1, lookbackYears));
            if (entries.Count == 0)
            {
                const string header = "Recent Elections";
                const string body = "No election results recorded.";
                return (header, body, body.GetHashCode());
            }

            int total = entries.Count;
            int displayCount = Mathf.Clamp(maxEntries, 1, total);

            builder.Clear();
            for (int i = 0; i < displayCount; i++)
            {
                var entry = entries[i];
                builder.Append(entry.Year).Append(':').Append(' ').Append(entry.OfficeName).Append(" — ");

                if (entry.Winners == null || entry.Winners.Count == 0)
                {
                    builder.Append("No winners recorded");
                }
                else
                {
                    int winnerCount = Mathf.Clamp(maxWinnersPerElection, 1, entry.Winners.Count);
                    for (int j = 0; j < winnerCount; j++)
                    {
                        var winner = entry.Winners[j];
                        string name = !string.IsNullOrEmpty(winner.CharacterName)
                            ? winner.CharacterName
                            : $"#{winner.CharacterId}";
                        int seatIndex = winner.SeatIndex >= 0 ? winner.SeatIndex + 1 : winner.SeatIndex;
                        builder.Append(name);
                        if (seatIndex > 0)
                        {
                            builder.Append(" (seat ").Append(seatIndex).Append(')');
                        }

                        if (!string.IsNullOrEmpty(winner.Notes))
                        {
                            builder.Append(" [").Append(winner.Notes).Append(']');
                        }

                        if (j < winnerCount - 1)
                            builder.Append(", ");
                    }

                    if (entry.Winners.Count > winnerCount)
                    {
                        builder.Append(", +").Append(entry.Winners.Count - winnerCount).Append(" more");
                    }
                }

                if (i < displayCount - 1)
                    builder.Append('\n');
            }

            if (total > displayCount)
            {
                builder.Append('\n').Append('+').Append(total - displayCount).Append(" more…");
            }

            string text = builder.ToString();
            builder.Clear();

            string headerText = total > displayCount
                ? $"Recent Elections ({displayCount}/{total})"
                : $"Recent Elections ({total})";

            return (headerText, text, text.GetHashCode());
        }

        private List<ElectionDisplayRow> GatherRecentElectionRows(int startYear, int lookbackYears)
        {
            var rows = new List<ElectionDisplayRow>();
            if (electionSystem == null)
                return rows;

            for (int year = startYear; year >= startYear - lookbackYears; year--)
            {
                var records = electionSystem.GetResultsForYear(year);
                if (records == null || records.Count == 0)
                    continue;

                for (int i = records.Count - 1; i >= 0; i--)
                {
                    var record = records[i];
                    if (record == null)
                        continue;

                    rows.Add(new ElectionDisplayRow
                    {
                        Year = record.Year,
                        OfficeName = record.Office?.Name ?? record.Office?.Id ?? "Office",
                        Rank = record.Office?.Rank ?? int.MinValue,
                        Winners = record.Winners ?? new List<ElectionWinnerSummary>()
                    });
                }
            }

            rows.Sort((a, b) =>
            {
                int yearCompare = b.Year.CompareTo(a.Year);
                if (yearCompare != 0)
                    return yearCompare;

                int rankCompare = b.Rank.CompareTo(a.Rank);
                if (rankCompare != 0)
                    return rankCompare;

                return string.Compare(a.OfficeName, b.OfficeName, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        private (string Text, int Count, DateTime LatestTimestamp) BuildLogsSection()
        {
            IReadOnlyList<Logger.LogEntry> entries = Logger.GetRecentEntries();
            if (entries == null || entries.Count == 0)
            {
                return (string.Empty, 0, DateTime.MinValue);
            }

            builder.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                builder.Append('[').Append(entry.Timestamp.ToString("HH:mm:ss"));
                builder.Append("] [").Append(entry.Category).Append("] ");
                builder.Append(entry.Message).Append('\n');
            }

            string text = builder.ToString();
            builder.Clear();

            var newestEntry = entries[entries.Count - 1];
            return (text, entries.Count, newestEntry.Timestamp);
        }

        private static float CalculateTicksPerSecond()
        {
            float delta = UnityEngine.Time.unscaledDeltaTime;
            if (delta <= 0f)
                return 0f;
            return 1f / delta;
        }

        private struct OfficeDisplayRow
        {
            public string OfficeId;
            public string OfficeName;
            public int Rank;
            public int SeatIndex;
            public string HolderName;
            public int StartYear;
            public int EndYear;
            public string PendingName;
            public int PendingStartYear;
        }

        private struct ElectionDisplayRow
        {
            public int Year;
            public string OfficeName;
            public int Rank;
            public List<ElectionWinnerSummary> Winners;
        }
    }
}
