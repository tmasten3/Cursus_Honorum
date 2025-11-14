using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using UnityEngine;

namespace Game.UI
{
    public sealed class DebugOverlayDataAdapter : IDisposable
    {
        public readonly struct SimulationData
        {
            public SimulationData(string dateLine, string tickRateLine, string speedLine, string stateLine)
            {
                DateLine = dateLine;
                TickRateLine = tickRateLine;
                SpeedLine = speedLine;
                StateLine = stateLine;
            }

            public string DateLine { get; }
            public string TickRateLine { get; }
            public string SpeedLine { get; }
            public string StateLine { get; }
        }

        public readonly struct PopulationData
        {
            public PopulationData(string livingLine, string familyLine, string todayLine, IReadOnlyList<string> historyLines)
            {
                LivingLine = livingLine;
                FamilyLine = familyLine;
                TodayLine = todayLine;
                HistoryLines = historyLines;
            }

            public string LivingLine { get; }
            public string FamilyLine { get; }
            public string TodayLine { get; }
            public IReadOnlyList<string> HistoryLines { get; }
        }

        public readonly struct PoliticsData
        {
            public PoliticsData(
                IReadOnlyList<string> currentOfficeLines,
                IReadOnlyList<string> upcomingElectionLines,
                IReadOnlyList<string> recentElectionResults,
                IReadOnlyList<string> recentAppointments)
            {
                CurrentOfficeLines = currentOfficeLines;
                UpcomingElectionLines = upcomingElectionLines;
                RecentElectionResults = recentElectionResults;
                RecentAppointments = recentAppointments;
            }

            public IReadOnlyList<string> CurrentOfficeLines { get; }
            public IReadOnlyList<string> UpcomingElectionLines { get; }
            public IReadOnlyList<string> RecentElectionResults { get; }
            public IReadOnlyList<string> RecentAppointments { get; }
        }

        public readonly struct Snapshot
        {
            public Snapshot(SimulationData simulation, PopulationData population, PoliticsData politics)
            {
                Simulation = simulation;
                Population = population;
                Politics = politics;
            }

            public SimulationData Simulation { get; }
            public PopulationData Population { get; }
            public PoliticsData Politics { get; }
        }

        private readonly TimeSystem timeSystem;
        private readonly CharacterSystem characterSystem;
        private readonly CharacterRepository characterRepository;
        private readonly OfficeSystem officeSystem;
        private readonly ElectionSystem electionSystem;
        private readonly EventBus eventBus;

        private readonly Queue<PopulationTickRecord> populationHistory = new();
        private readonly List<string> recentElectionResults = new();
        private readonly List<string> recentAppointments = new();

        private int todaysBirths;
        private int todaysDeaths;
        private int todaysMarriages;
        private int currentDayKey;

        private float lastSampleTime = -1f;
        private int lastSampledDayCount;
        private float lastCalculatedSpeed;

        private bool subscribed;

        private const int MaxPopulationHistory = 30;
        private const int MaxElectionResults = 8;
        private const int MaxAppointments = 12;
        private const int MaxOfficeLines = 12;
        private const int MaxUpcomingElections = 10;

        private static readonly int[] DaysInMonth =
        {
            31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31
        };

        private readonly StringBuilder builder = new();

        public DebugOverlayDataAdapter(
            TimeSystem timeSystem,
            CharacterSystem characterSystem,
            CharacterRepository characterRepository,
            OfficeSystem officeSystem,
            ElectionSystem electionSystem,
            EventBus eventBus)
        {
            this.timeSystem = timeSystem;
            this.characterSystem = characterSystem;
            this.characterRepository = characterRepository;
            this.officeSystem = officeSystem;
            this.electionSystem = electionSystem;
            this.eventBus = eventBus;
        }

        public void Initialize()
        {
            if (eventBus == null || subscribed)
                return;

            eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Subscribe<OnPopulationTick>(OnPopulationTick);
            eventBus.Subscribe<OnCharacterBorn>(OnCharacterBorn);
            eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);
            eventBus.Subscribe<OnCharacterMarried>(OnCharacterMarried);
            eventBus.Subscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
            eventBus.Subscribe<OfficeAssignedEvent>(OnOfficeAssigned);

            subscribed = true;
        }

        public Snapshot CreateSnapshot()
        {
            var simulation = BuildSimulationData();
            var population = BuildPopulationData();
            var politics = BuildPoliticsData();
            return new Snapshot(simulation, population, politics);
        }

        public void Dispose()
        {
            if (eventBus != null && subscribed)
            {
                eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
                eventBus.Unsubscribe<OnPopulationTick>(OnPopulationTick);
                eventBus.Unsubscribe<OnCharacterBorn>(OnCharacterBorn);
                eventBus.Unsubscribe<OnCharacterDied>(OnCharacterDied);
                eventBus.Unsubscribe<OnCharacterMarried>(OnCharacterMarried);
                eventBus.Unsubscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
                eventBus.Unsubscribe<OfficeAssignedEvent>(OnOfficeAssigned);
            }

            subscribed = false;
            populationHistory.Clear();
            recentElectionResults.Clear();
            recentAppointments.Clear();
            todaysBirths = todaysDeaths = todaysMarriages = 0;
            currentDayKey = 0;
            lastSampleTime = -1f;
            lastSampledDayCount = 0;
            lastCalculatedSpeed = 0f;
        }

        private SimulationData BuildSimulationData()
        {
            string dateLine = timeSystem != null
                ? $"Date: {timeSystem.GetCurrentDateString()}"
                : "Date: unavailable";

            string tickRateLine = $"Ticks / sec: {CalculateTickRate():F1}";
            string speedLine = FormatSimulationSpeed(UpdateSimulationSpeed());
            string stateLine = timeSystem != null && timeSystem.IsPaused ? "State: Paused" : "State: Running";

            return new SimulationData(dateLine, tickRateLine, speedLine, stateLine);
        }

        private PopulationData BuildPopulationData()
        {
            int living = characterRepository?.AliveCount ?? characterSystem?.CountAlive() ?? 0;
            int families = characterRepository?.FamilyCount ?? characterSystem?.GetFamilyCount() ?? 0;

            string livingLine = $"Total Living Characters: {living:N0}";
            string familyLine = $"Total Families: {families:N0}";

            var todayDate = timeSystem?.GetCurrentDate();
            string todayPrefix = todayDate.HasValue
                ? $"Today ({FormatDate(todayDate.Value.year, todayDate.Value.month, todayDate.Value.day)}):"
                : "Today:";

            string todayLine = $"{todayPrefix} Births {todaysBirths}, Deaths {todaysDeaths}, Marriages {todaysMarriages}";

            var historyLines = BuildPopulationHistoryLines();
            return new PopulationData(livingLine, familyLine, todayLine, historyLines);
        }

        private PoliticsData BuildPoliticsData()
        {
            var offices = BuildCurrentOfficeLines();
            var elections = BuildUpcomingElectionLines();

            var results = new List<string>(recentElectionResults);
            if (results.Count == 0)
            {
                var historical = BuildHistoricalElectionResults();
                if (historical.Count > 0)
                    results = historical;
            }

            var appointments = new List<string>(recentAppointments);

            return new PoliticsData(offices, elections, results, appointments);
        }

        private IReadOnlyList<string> BuildPopulationHistoryLines()
        {
            if (populationHistory.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(populationHistory.Count);
            foreach (var record in populationHistory)
            {
                string date = FormatDate(record.Year, record.Month, record.Day);
                lines.Add($"{date} — Births {record.Births}, Deaths {record.Deaths}, Marriages {record.Marriages}");
            }

            return lines;
        }

        private List<string> BuildCurrentOfficeLines()
        {
            var lines = new List<string>();

            if (officeSystem == null || characterSystem == null)
                return lines;

            var definitions = officeSystem.GetAllDefinitions();
            var living = characterSystem.GetAllLiving();

            if (definitions == null || living == null || living.Count == 0)
                return lines;

            var definitionMap = definitions
                .Where(def => def != null && !string.IsNullOrEmpty(def.Id))
                .ToDictionary(def => def.Id, def => def, StringComparer.OrdinalIgnoreCase);

            var rows = new List<OfficeRow>();
            foreach (var character in living)
            {
                if (character == null)
                    continue;

                var holdings = officeSystem.GetCurrentHoldings(character.ID);
                if (holdings == null || holdings.Count == 0)
                    continue;

                foreach (var seat in holdings)
                {
                    if (seat == null || !seat.HolderId.HasValue)
                        continue;

                    var row = new OfficeRow
                    {
                        OfficeId = seat.OfficeId ?? string.Empty,
                        SeatIndex = seat.SeatIndex,
                        HolderName = !string.IsNullOrEmpty(character.FullName) ? character.FullName : $"#{seat.HolderId.Value}",
                        StartYear = seat.StartYear,
                        EndYear = seat.EndYear,
                        PendingHolderId = seat.PendingHolderId,
                        PendingStartYear = seat.PendingStartYear
                    };

                    if (definitionMap.TryGetValue(row.OfficeId, out var definition))
                    {
                        row.OfficeName = definition.Name;
                        row.Rank = definition.Rank;
                    }
                    else
                    {
                        row.OfficeName = string.IsNullOrEmpty(row.OfficeId) ? "Office" : row.OfficeId;
                        row.Rank = int.MinValue;
                    }

                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
                return lines;

            rows.Sort(CompareOfficeRows);

            int displayCount = Mathf.Min(rows.Count, MaxOfficeLines);
            for (int i = 0; i < displayCount; i++)
            {
                var row = rows[i];

                builder.Clear();
                builder.Append(row.OfficeName);
                if (row.SeatIndex >= 0)
                {
                    builder.Append(' ');
                    builder.Append('#').Append(row.SeatIndex + 1);
                }

                builder.Append(':').Append(' ').Append(row.HolderName);

                string term = FormatTerm(row.StartYear, row.EndYear);
                if (!string.IsNullOrEmpty(term))
                {
                    builder.Append(" (term ").Append(term).Append(')');
                }

                if (row.PendingHolderId.HasValue)
                {
                    string pendingName = ResolveCharacterName(row.PendingHolderId.Value);
                    builder.Append(" → ").Append(pendingName);
                    if (row.PendingStartYear > 0)
                        builder.Append(" (from ").Append(row.PendingStartYear).Append(')');
                }

                lines.Add(builder.ToString());
                builder.Clear();
            }

            if (rows.Count > displayCount)
            {
                lines.Add($"…and {rows.Count - displayCount} more assignments");
            }

            return lines;
        }

        private List<string> BuildUpcomingElectionLines()
        {
            var lines = new List<string>();

            if (officeSystem == null || timeSystem == null)
                return lines;

            var current = timeSystem.GetCurrentDate();
            var years = new HashSet<int> { current.year, current.year + 1 };

            foreach (var year in years.OrderBy(y => y))
            {
                var infos = officeSystem.GetElectionInfos(year);
                if (infos == null || infos.Count == 0)
                    continue;

                foreach (var info in infos)
                {
                    string name = info.Definition?.Name ?? info.Definition?.Id ?? "Office";
                    int seats = Mathf.Max(1, info.SeatsAvailable);
                    builder.Clear();
                    builder.Append(year).Append(':').Append(' ').Append(name).Append(' ');
                    builder.Append('(').Append(seats).Append(' ');
                    builder.Append(seats == 1 ? "seat" : "seats").Append(')');

                    lines.Add(builder.ToString());
                    builder.Clear();

                    if (lines.Count >= MaxUpcomingElections)
                        return lines;
                }
            }

            return lines;
        }

        private List<string> BuildHistoricalElectionResults()
        {
            var lines = new List<string>();

            if (electionSystem == null || timeSystem == null)
                return lines;

            var current = timeSystem.GetCurrentDate();
            int startYear = current.year;
            int minYear = startYear - 5;

            for (int year = startYear; year >= minYear; year--)
            {
                var records = electionSystem.GetResultsForYear(year);
                if (records == null || records.Count == 0)
                    continue;

                for (int i = records.Count - 1; i >= 0; i--)
                {
                    var record = records[i];
                    if (record == null)
                        continue;

                    builder.Clear();
                    builder.Append(year).Append(':').Append(' ');
                    builder.Append(record.Office?.Name ?? record.Office?.Id ?? "Office").Append(' ');

                    if (record.Winners == null || record.Winners.Count == 0)
                    {
                        builder.Append("— No winners recorded");
                    }
                    else
                    {
                        var winners = record.Winners
                            .Where(w => w != null)
                            .Take(3)
                            .Select(FormatWinner);
                        builder.Append("— ").Append(string.Join(", ", winners));

                        if (record.Winners.Count > 3)
                            builder.Append(", +").Append(record.Winners.Count - 3).Append(" more");
                    }

                    lines.Add(builder.ToString());
                    builder.Clear();

                    if (lines.Count >= MaxElectionResults)
                        return lines;
                }
            }

            return lines;
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            currentDayKey = BuildDayKey(e.Year, e.Month, e.Day);
            todaysBirths = todaysDeaths = todaysMarriages = 0;
        }

        private void OnPopulationTick(OnPopulationTick e)
        {
            todaysBirths = e.Births;
            todaysDeaths = e.Deaths;
            todaysMarriages = e.Marriages;
            currentDayKey = BuildDayKey(e.Year, e.Month, e.Day);

            var record = new PopulationTickRecord(e.Year, e.Month, e.Day, e.Births, e.Deaths, e.Marriages);
            populationHistory.Enqueue(record);
            while (populationHistory.Count > MaxPopulationHistory)
                populationHistory.Dequeue();
        }

        private void OnCharacterBorn(OnCharacterBorn e)
        {
            EnsureCurrentDay(e.Year, e.Month, e.Day);
            todaysBirths++;
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            EnsureCurrentDay(e.Year, e.Month, e.Day);
            todaysDeaths++;
        }

        private void OnCharacterMarried(OnCharacterMarried e)
        {
            EnsureCurrentDay(e.Year, e.Month, e.Day);
            todaysMarriages++;
        }

        private void OnElectionSeasonCompleted(ElectionSeasonCompletedEvent e)
        {
            if (e?.Results == null || e.Results.Count == 0)
                return;

            foreach (var summary in e.Results)
            {
                if (summary == null)
                    continue;

                builder.Clear();
                builder.Append(e.ElectionYear).Append(':').Append(' ');
                builder.Append(summary.OfficeName ?? summary.OfficeId ?? "Office").Append(' ');

                if (summary.Winners == null || summary.Winners.Count == 0)
                {
                    builder.Append("— No winners recorded");
                }
                else
                {
                    var winners = summary.Winners
                        .Where(w => w != null)
                        .Take(3)
                        .Select(FormatWinner);
                    builder.Append("— ").Append(string.Join(", ", winners));

                    if (summary.Winners.Count > 3)
                        builder.Append(", +").Append(summary.Winners.Count - 3).Append(" more");
                }

                PushToRollingList(recentElectionResults, builder.ToString(), MaxElectionResults);
                builder.Clear();
            }
        }

        private void OnOfficeAssigned(OfficeAssignedEvent e)
        {
            if (e == null)
                return;

            builder.Clear();
            builder.Append(FormatDate(e.Year, e.Month, e.Day)).Append(':').Append(' ');
            builder.Append(!string.IsNullOrEmpty(e.CharacterName) ? e.CharacterName : $"#{e.CharacterId}");
            builder.Append(" → ");
            builder.Append(string.IsNullOrEmpty(e.OfficeName) ? e.OfficeId : e.OfficeName);
            if (e.SeatIndex >= 0)
                builder.Append(" #").Append(e.SeatIndex + 1);

            string term = FormatTerm(e.TermStartYear, e.TermEndYear);
            if (!string.IsNullOrEmpty(term))
            {
                builder.Append(" (term ").Append(term).Append(')');
            }

            PushToRollingList(recentAppointments, builder.ToString(), MaxAppointments);
            builder.Clear();
        }

        private void EnsureCurrentDay(int year, int month, int day)
        {
            int key = BuildDayKey(year, month, day);
            if (key == currentDayKey)
                return;

            currentDayKey = key;
            todaysBirths = todaysDeaths = todaysMarriages = 0;
        }

        private static int BuildDayKey(int year, int month, int day)
        {
            return (((year * 100) + month) * 100) + day;
        }

        private float CalculateTickRate()
        {
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f)
                return 0f;

            return 1f / delta;
        }

        private float UpdateSimulationSpeed()
        {
            if (timeSystem == null)
                return 0f;

            var current = timeSystem.GetCurrentDate();
            int totalDays = ToAbsoluteDayCount(current.year, current.month, current.day);
            float now = Time.unscaledTime;

            if (lastSampleTime < 0f)
            {
                lastSampleTime = now;
                lastSampledDayCount = totalDays;
                lastCalculatedSpeed = 0f;
                return lastCalculatedSpeed;
            }

            float deltaTime = now - lastSampleTime;
            int dayDelta = totalDays - lastSampledDayCount;

            if (deltaTime > 0.0001f && dayDelta >= 0)
            {
                lastCalculatedSpeed = dayDelta / deltaTime;
            }

            lastSampleTime = now;
            lastSampledDayCount = totalDays;
            return lastCalculatedSpeed;
        }

        private static int ToAbsoluteDayCount(int year, int month, int day)
        {
            int total = year * 365;
            int monthIndex = Mathf.Clamp(month, 1, 12) - 1;
            for (int i = 0; i < monthIndex; i++)
                total += DaysInMonth[i];

            total += Mathf.Max(0, day - 1);
            return total;
        }

        private static string FormatSimulationSpeed(float daysPerSecond)
        {
            return $"Sim Speed: {daysPerSecond:F2} days/sec";
        }

        private static string FormatDate(int year, int month, int day)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:00}-{2:00}", year, month, day);
        }

        private static string FormatWinner(ElectionWinnerSummary winner)
        {
            if (winner == null)
                return string.Empty;

            string name = !string.IsNullOrEmpty(winner.CharacterName) ? winner.CharacterName : $"#{winner.CharacterId}";
            if (winner.SeatIndex >= 0)
                return string.Format(CultureInfo.InvariantCulture, "{0} (seat {1})", name, winner.SeatIndex + 1);
            return name;
        }

        private string ResolveCharacterName(int characterId)
        {
            var character = characterSystem?.Get(characterId) ?? characterRepository?.Get(characterId);
            return !string.IsNullOrEmpty(character?.FullName) ? character.FullName : $"#{characterId}";
        }

        private static string FormatTerm(int startYear, int endYear)
        {
            if (startYear <= 0 && endYear <= 0)
                return string.Empty;
            if (startYear <= 0)
                return endYear.ToString(CultureInfo.InvariantCulture);
            if (endYear <= 0 || endYear == startYear)
                return startYear.ToString(CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}", startYear, endYear);
        }

        private static void PushToRollingList(List<string> list, string entry, int capacity)
        {
            if (string.IsNullOrEmpty(entry))
                return;

            list.Add(entry);
            while (list.Count > capacity)
                list.RemoveAt(0);
        }

        private static int CompareOfficeRows(OfficeRow a, OfficeRow b)
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

        private readonly struct PopulationTickRecord
        {
            public PopulationTickRecord(int year, int month, int day, int births, int deaths, int marriages)
            {
                Year = year;
                Month = month;
                Day = day;
                Births = births;
                Deaths = deaths;
                Marriages = marriages;
            }

            public int Year { get; }
            public int Month { get; }
            public int Day { get; }
            public int Births { get; }
            public int Deaths { get; }
            public int Marriages { get; }
        }

        private struct OfficeRow
        {
            public string OfficeId;
            public string OfficeName;
            public int Rank;
            public int SeatIndex;
            public string HolderName;
            public int StartYear;
            public int EndYear;
            public int? PendingHolderId;
            public int PendingStartYear;
        }
    }
}
