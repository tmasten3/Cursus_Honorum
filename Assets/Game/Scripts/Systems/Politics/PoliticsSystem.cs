using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace Game.Systems.Politics
{
    public sealed class PoliticsSystem : GameSystemBase
    {
        public override string Name => "Politics System";
        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(EventBus.EventBus),
            typeof(TimeSystem),
            typeof(Game.Systems.CharacterSystem.CharacterSystem),
            typeof(OfficeSystem),
            typeof(ElectionSystem)
        };

        private readonly EventBus.EventBus eventBus;
        private readonly TimeSystem timeSystem;
        private readonly Game.Systems.CharacterSystem.CharacterSystem characterSystem;
        private readonly OfficeSystem officeSystem;
        private readonly ElectionSystem electionSystem;

        private readonly PoliticsTermTracker termTracker = new();
        private readonly Dictionary<int, List<string>> eligibilityByCharacter = new();
        private readonly Dictionary<(int Month, int Day), HashSet<int>> birthdayIndex = new();
        private readonly PoliticsModelFactory.ElectionCycleState cycleState = new();
        private readonly HashSet<int> pendingTermHistoryRefresh = new();

        private bool subscriptionsActive;
        private int currentYear;

        public PoliticsSystem(EventBus.EventBus eventBus, TimeSystem timeSystem,
            Game.Systems.CharacterSystem.CharacterSystem characterSystem,
            OfficeSystem officeSystem, ElectionSystem electionSystem)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            this.officeSystem = officeSystem ?? throw new ArgumentNullException(nameof(officeSystem));
            this.electionSystem = electionSystem ?? throw new ArgumentNullException(nameof(electionSystem));
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);

            if (!subscriptionsActive)
            {
                eventBus.Subscribe<OnNewYearEvent>(OnNewYear);
                eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
                eventBus.Subscribe<ElectionSeasonOpenedEvent>(OnElectionSeasonOpened);
                eventBus.Subscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
                eventBus.Subscribe<OfficeAssignedEvent>(OnOfficeAssigned);
                eventBus.Subscribe<OnCharacterBorn>(OnCharacterBorn);
                eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);
                eventBus.Subscribe<OnPopulationTick>(OnPopulationTick);
                subscriptionsActive = true;
            }

            var (year, _, _) = timeSystem.GetCurrentDate();
            currentYear = year;
            cycleState.Reset(year);

            SeedTermHistory();
            RefreshEligibility(year);

            LogInfo($"Politics system initialized for {year}. Tracking {eligibilityByCharacter.Count} eligible citizens (election engine: {electionSystem.Name}).");
        }

        public override void Update(GameState state) { }

        public override void Shutdown()
        {
            if (subscriptionsActive)
            {
                eventBus.Unsubscribe<OnNewYearEvent>(OnNewYear);
                eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
                eventBus.Unsubscribe<ElectionSeasonOpenedEvent>(OnElectionSeasonOpened);
                eventBus.Unsubscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
                eventBus.Unsubscribe<OfficeAssignedEvent>(OnOfficeAssigned);
                eventBus.Unsubscribe<OnCharacterBorn>(OnCharacterBorn);
                eventBus.Unsubscribe<OnCharacterDied>(OnCharacterDied);
                eventBus.Unsubscribe<OnPopulationTick>(OnPopulationTick);
                subscriptionsActive = false;
            }

            eligibilityByCharacter.Clear();
            birthdayIndex.Clear();
            termTracker.Reset();
            pendingTermHistoryRefresh.Clear();

            base.Shutdown();
        }

        public ElectionCycleSnapshot GetCurrentElectionCycle()
        {
            return PoliticsModelFactory.CreateCycleSnapshot(cycleState);
        }

        public PoliticsEligibilitySnapshot GetEligibilitySnapshot(int characterId)
        {
            if (!eligibilityByCharacter.TryGetValue(characterId, out var offices))
            {
                return new PoliticsEligibilitySnapshot(currentYear, Array.Empty<string>());
            }

            return new PoliticsEligibilitySnapshot(currentYear, new ReadOnlyCollection<string>(offices));
        }

        public IReadOnlyList<OfficeTermRecord> GetTermHistory(int characterId)
        {
            return termTracker.GetHistory(characterId);
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            currentYear = e.Year;
            cycleState.Reset(currentYear);
            RefreshEligibility(currentYear);
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            if (e == null)
                return;

            currentYear = e.Year;
            ProcessPendingTermRefresh();
        }

        private void OnPopulationTick(OnPopulationTick e)
        {
            if (e == null)
                return;

            currentYear = e.Year;

            var key = (e.Month, e.Day);
            if (!birthdayIndex.TryGetValue(key, out var ids) || ids.Count == 0)
            {
                ProcessPendingTermRefresh();
                return;
            }

            var toUpdate = new List<int>(ids);
            foreach (var characterId in toUpdate)
            {
                UpdateEligibilityForCharacter(characterId);
            }

            ProcessPendingTermRefresh();
        }

        private void OnElectionSeasonOpened(ElectionSeasonOpenedEvent e)
        {
            if (e == null)
                return;

            if (e.ElectionYear != currentYear)
            {
                currentYear = e.ElectionYear;
                cycleState.Reset(currentYear);
            }

            cycleState.MarkSeasonOpened(e.Month, e.Day, e.Offices);
            LogInfo($"Election season opened for {currentYear} covering {cycleState.Offices.Count} magistracies.");
        }

        private void OnElectionSeasonCompleted(ElectionSeasonCompletedEvent e)
        {
            if (e == null)
                return;

            if (e.ElectionYear != currentYear)
            {
                currentYear = e.ElectionYear;
                cycleState.Reset(currentYear);
            }

            cycleState.MarkSeasonCompleted(e.Month, e.Day, e.Results);
            LogInfo($"Election season {currentYear} concluded with {cycleState.Results.Count} result bundles.");

            if (e.Results == null)
                return;

            foreach (var summary in e.Results)
            {
                if (summary?.Winners == null)
                    continue;

                foreach (var winner in summary.Winners)
                {
                    UpdateEligibilityForCharacter(winner.CharacterId);
                }
            }
        }

        private void OnOfficeAssigned(OfficeAssignedEvent e)
        {
            if (e == null)
                return;

            termTracker.RecordAssignment(e);
            UpdateEligibilityForCharacter(e.CharacterId);
        }

        private void RefreshEligibility(int year)
        {
            eligibilityByCharacter.Clear();
            birthdayIndex.Clear();

            var living = characterSystem.GetAllLiving();
            if (living == null)
                return;

            foreach (var character in living)
            {
                if (character == null)
                    continue;

                EnsureBirthdayTracked(character);
                var eligible = officeSystem.GetEligibleOffices(character, year);
                StoreEligibilitySnapshot(character.ID, eligible);
            }
        }

        private void UpdateEligibilityForCharacter(int characterId)
        {
            var character = characterSystem.Get(characterId);
            if (character == null)
            {
                eligibilityByCharacter.Remove(characterId);
                RemoveBirthdayEntry(characterId);
                return;
            }

            if (!character.IsAlive)
            {
                eligibilityByCharacter.Remove(characterId);
                RemoveBirthdayEntry(character);
                return;
            }

            EnsureBirthdayTracked(character);
            var eligible = officeSystem.GetEligibleOffices(character, currentYear);
            StoreEligibilitySnapshot(characterId, eligible);
        }

        private void StoreEligibilitySnapshot(int characterId, IReadOnlyList<OfficeDefinition> definitions)
        {
            if (definitions == null)
            {
                eligibilityByCharacter.Remove(characterId);
                return;
            }

            var offices = new List<string>();
            foreach (var definition in definitions)
            {
                if (definition?.Id == null)
                    continue;

                offices.Add(definition.Id);
            }

            eligibilityByCharacter[characterId] = offices;
        }

        private void SeedTermHistory()
        {
            termTracker.Reset();
            pendingTermHistoryRefresh.Clear();

            var living = characterSystem.GetAllLiving();
            if (living == null)
                return;

            foreach (var character in living)
            {
                if (character == null)
                    continue;

                var history = officeSystem.GetCareerHistory(character.ID);
                if (history != null && history.Count > 0)
                {
                    termTracker.SeedFromHistory(character.ID, history, ResolveOfficeName);
                }

                var holdings = officeSystem.GetCurrentHoldings(character.ID);
                if (holdings == null)
                    continue;

                foreach (var seat in holdings)
                {
                    termTracker.SeedActiveAssignment(character.ID, seat, ResolveOfficeName);
                }
            }
        }

        private string ResolveOfficeName(string officeId)
        {
            var definition = officeSystem.GetDefinition(officeId);
            return definition?.Name ?? officeId;
        }

        private void EnsureBirthdayTracked(Game.Data.Characters.Character character)
        {
            if (character == null)
                return;

            var key = (character.BirthMonth, character.BirthDay);
            if (!birthdayIndex.TryGetValue(key, out var ids))
            {
                ids = new HashSet<int>();
                birthdayIndex[key] = ids;
            }

            ids.Add(character.ID);
        }

        private void RemoveBirthdayEntry(int characterId)
        {
            var character = characterSystem.Get(characterId);
            RemoveBirthdayEntry(character);
        }

        private void RemoveBirthdayEntry(Game.Data.Characters.Character character)
        {
            if (character == null)
                return;

            var key = (character.BirthMonth, character.BirthDay);
            if (!birthdayIndex.TryGetValue(key, out var ids))
                return;

            ids.Remove(character.ID);
            if (ids.Count == 0)
                birthdayIndex.Remove(key);
        }

        private void OnCharacterBorn(OnCharacterBorn e)
        {
            if (e == null)
                return;

            var character = characterSystem.Get(e.ChildID);
            if (character == null || !character.IsAlive)
                return;

            EnsureBirthdayTracked(character);
            UpdateEligibilityForCharacter(e.ChildID);
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            if (e == null)
                return;

            RemoveBirthdayEntry(e.CharacterID);
            eligibilityByCharacter.Remove(e.CharacterID);
            QueueTermHistoryRefresh(e.CharacterID);
        }

        private void QueueTermHistoryRefresh(int characterId)
        {
            if (characterId <= 0)
                return;

            pendingTermHistoryRefresh.Add(characterId);
        }

        private void ProcessPendingTermRefresh()
        {
            if (pendingTermHistoryRefresh.Count == 0)
                return;

            var toRefresh = new List<int>(pendingTermHistoryRefresh);
            pendingTermHistoryRefresh.Clear();

            foreach (var characterId in toRefresh)
            {
                RefreshTermHistoryForCharacter(characterId);
            }
        }

        private void RefreshTermHistoryForCharacter(int characterId)
        {
            if (characterId <= 0)
                return;

            var history = officeSystem.GetCareerHistory(characterId);
            var holdings = officeSystem.GetCurrentHoldings(characterId);
            termTracker.RebuildCharacterHistory(characterId, history, holdings, ResolveOfficeName);
        }
    }
}
