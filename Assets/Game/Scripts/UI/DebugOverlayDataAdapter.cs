using System;
using System.Collections.Generic;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Supplies lightweight data objects so the debug overlay can render without
    /// taking hard dependencies on simulation internals.
    /// </summary>
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
                HistoryLines = historyLines ?? Array.Empty<string>();
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
                CurrentOfficeLines = currentOfficeLines ?? Array.Empty<string>();
                UpcomingElectionLines = upcomingElectionLines ?? Array.Empty<string>();
                RecentElectionResults = recentElectionResults ?? Array.Empty<string>();
                RecentAppointments = recentAppointments ?? Array.Empty<string>();
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

        private bool subscribed;
        private int todaysBirths;
        private int todaysDeaths;
        private int todaysMarriages;
        private int currentDayKey = -1;

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
            }

            subscribed = false;
            todaysBirths = todaysDeaths = todaysMarriages = 0;
            currentDayKey = -1;
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            currentDayKey = ComposeDayKey(e.Year, e.Month, e.Day);
            todaysBirths = todaysDeaths = todaysMarriages = 0;
        }

        private void OnPopulationTick(OnPopulationTick e)
        {
            if (!IsCurrentDay(e.Year, e.Month, e.Day))
                currentDayKey = ComposeDayKey(e.Year, e.Month, e.Day);

            todaysBirths = e.Births;
            todaysDeaths = e.Deaths;
            todaysMarriages = e.Marriages;
        }

        private void OnCharacterBorn(OnCharacterBorn e)
        {
            if (IsCurrentDay(e.Year, e.Month, e.Day))
                todaysBirths++;
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            if (IsCurrentDay(e.Year, e.Month, e.Day))
                todaysDeaths++;
        }

        private void OnCharacterMarried(OnCharacterMarried e)
        {
            if (IsCurrentDay(e.Year, e.Month, e.Day))
                todaysMarriages++;
        }

        private bool IsCurrentDay(int year, int month, int day)
        {
            return currentDayKey == ComposeDayKey(year, month, day);
        }

        private static int ComposeDayKey(int year, int month, int day)
        {
            return ((year * 100) + month) * 100 + day;
        }

        private SimulationData BuildSimulationData()
        {
            string dateLine = timeSystem != null
                ? $"Date: {timeSystem.GetCurrentDateString()}"
                : "Date: unavailable";

            string tickRateLine = "Tick Rate: n/a";
            string speedLine = timeSystem != null && timeSystem.IsPaused ? "Speed: 0x" : "Speed: 1x";
            string stateLine = timeSystem != null && timeSystem.IsPaused ? "State: Paused" : "State: Running";

            return new SimulationData(dateLine, tickRateLine, speedLine, stateLine);
        }

        private PopulationData BuildPopulationData()
        {
            int living = characterRepository?.AliveCount ?? characterSystem?.CountAlive() ?? 0;
            int families = characterRepository?.FamilyCount ?? characterSystem?.GetFamilyCount() ?? 0;

            string livingLine = $"Living Characters: {living:N0}";
            string familyLine = $"Families: {families:N0}";
            string todayLine = $"Today: Births {todaysBirths}, Deaths {todaysDeaths}, Marriages {todaysMarriages}";

            return new PopulationData(livingLine, familyLine, todayLine, Array.Empty<string>());
        }

        private PoliticsData BuildPoliticsData()
        {
            var officeLines = BuildCurrentOfficeLines();
            var electionLines = BuildUpcomingElectionLines();

            return new PoliticsData(officeLines, electionLines, Array.Empty<string>(), Array.Empty<string>());
        }

        private IReadOnlyList<string> BuildCurrentOfficeLines()
        {
            if (officeSystem == null)
                return Array.Empty<string>();

            var definitions = officeSystem.GetAllDefinitions();
            if (definitions == null || definitions.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>();
            int limit = Mathf.Min(definitions.Count, 6);

            for (int i = 0; i < limit; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                string name = !string.IsNullOrEmpty(definition.Name) ? definition.Name : definition.Id;
                lines.Add($"• {name}");
            }

            if (definitions.Count > limit)
            {
                lines.Add($"…and {definitions.Count - limit} more office types");
            }

            return lines;
        }

        private IReadOnlyList<string> BuildUpcomingElectionLines()
        {
            if (officeSystem == null || timeSystem == null)
                return Array.Empty<string>();

            var currentDate = timeSystem.GetCurrentDate();
            var infos = officeSystem.GetElectionInfos(currentDate.year);
            if (infos == null || infos.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>();
            foreach (var info in infos)
            {
                if (info?.Definition == null)
                    continue;

                int seats = Mathf.Max(1, info.SeatsAvailable);
                string name = !string.IsNullOrEmpty(info.Definition.Name)
                    ? info.Definition.Name
                    : info.Definition.Id ?? "Office";

                lines.Add($"{currentDate.year}: {name} ({seats} {(seats == 1 ? "seat" : "seats")})");

                if (lines.Count >= 6)
                    break;
            }

            return lines;
        }
    }
}
