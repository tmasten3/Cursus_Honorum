using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;

namespace Game.Systems.Politics.Offices
{
    public class OfficeSystem : GameSystemBase
    {
        public override string Name => "Office System";
        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(EventBus.EventBus),
            typeof(Game.Systems.CharacterSystem.CharacterSystem)
        };

        private readonly EventBus.EventBus eventBus;
        private readonly Game.Systems.CharacterSystem.CharacterSystem characterSystem;

        private readonly OfficeDefinitions definitions;
        private readonly OfficeStateService state;
        private readonly OfficeEligibilityService eligibility;
        private readonly IMagistrateOfficeRepository repository;

        private readonly Dictionary<int, int> historySeedCursor = new Dictionary<int, int>();
        private readonly HashSet<(int characterId, string officeId)> seededHistoryRecords =
            new HashSet<(int characterId, string officeId)>();

        private int currentYear;
        private int currentMonth;
        private int currentDay;

        private bool initialHoldersSeeded;
        private EventSubscription newDaySubscription = EventSubscription.Empty;
        private EventSubscription characterDiedSubscription = EventSubscription.Empty;

        public bool DebugMode { get; set; }

        public int TotalOfficesCount => definitions.Count;

        public OfficeDefinitions Definitions => definitions;

        public OfficeStateService StateService => state;

        public OfficeEligibilityService EligibilityService => eligibility;

        public OfficeSystem(EventBus.EventBus eventBus, Game.Systems.CharacterSystem.CharacterSystem characterSystem,
            IMagistrateOfficeRepository repository = null)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            string defaultPath = System.IO.Path.Combine("Assets", "Game", "Data", "MagistrateOffices.json");
            this.repository = repository ?? new JsonMagistrateOfficeRepository(defaultPath, LogWarn, LogError);

            definitions = new OfficeDefinitions(LogInfo, LogWarn, LogError);
            state = new OfficeStateService(LogWarn);
            eligibility = new OfficeEligibilityService(state);
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            var collection = repository.Load();
            definitions.LoadDefinitions(collection);
            this.state.EnsureSeatStructures(definitions.GetAllDefinitions());

            newDaySubscription.Dispose();
            characterDiedSubscription.Dispose();

            newDaySubscription = eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
            characterDiedSubscription = eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);

            SeedInitialOfficeHolders();
        }

        public override void Shutdown()
        {
            newDaySubscription.Dispose();
            characterDiedSubscription.Dispose();
            newDaySubscription = EventSubscription.Empty;
            characterDiedSubscription = EventSubscription.Empty;

            base.Shutdown();
        }

        private void SeedInitialOfficeHolders()
        {
            if (initialHoldersSeeded)
                return;

            if (this.state.AnySeatsFilled())
            {
                initialHoldersSeeded = true;
                return;
            }

            int initialYear = EstimateInitialYear();
            currentYear = initialYear;
            currentMonth = 1;
            currentDay = 1;

            historySeedCursor.Clear();
            seededHistoryRecords.Clear();

            var living = characterSystem.GetAllLiving()
                ?.Where(c => c != null && c.IsAlive && c.IsMale)
                .OrderByDescending(c => c.Age)
                .ThenBy(c => c.ID)
                .ToList() ?? new List<Character>();

            var assignedCharacters = new HashSet<int>();
            var orderedDefinitions = definitions.GetAllDefinitions()
                .OrderByDescending(d => d.Rank)
                .ThenByDescending(d => d.MinAge)
                .ToList();

            foreach (var def in orderedDefinitions)
            {
                if (def == null)
                    continue;

                var seats = this.state.GetOrCreateSeatList(def.Id, def.Seats);
                if (seats == null || seats.Count == 0)
                    continue;

                for (int i = 0; i < seats.Count; i++)
                {
                    var candidate = FindCandidateForOffice(def, initialYear, assignedCharacters, living);
                    if (candidate == null)
                    {
                        LogWarn($"Unable to seed {def.Id} seat {seats[i].SeatIndex}: no eligible characters available.");
                        continue;
                    }

                    assignedCharacters.Add(candidate.ID);

                    int termStart = initialYear - Math.Max(0, def.TermLengthYears - 1);
                    int termEnd = initialYear;

                    bool assigned = this.state.TryAssignInitialHolder(def.Id, seats[i].SeatIndex, candidate.ID, termStart, termEnd);
                    if (!assigned)
                        continue;

                    if (DebugMode)
                    {
                        string name = ResolveCharacterName(candidate.ID);
                        LogInfo($"Seeded {name}#{candidate.ID} to {def.Name} seat {seats[i].SeatIndex} ({FormatTermRange(termStart, termEnd)}).");
                    }
                }
            }

            initialHoldersSeeded = true;
        }

        private Character FindCandidateForOffice(OfficeDefinition definition, int evaluationYear,
            HashSet<int> assignedCharacters, List<Character> living)
        {
            foreach (var candidate in living)
            {
                if (candidate == null || assignedCharacters.Contains(candidate.ID))
                    continue;

                if (!MeetsBaseEligibility(candidate, definition))
                    continue;

                if (!eligibility.IsEligible(candidate, definition, evaluationYear, out _))
                {
                    EnsurePrerequisiteHistory(candidate, definition, evaluationYear);
                    if (!eligibility.IsEligible(candidate, definition, evaluationYear, out _))
                        continue;
                }

                return candidate;
            }

            return null;
        }

        private bool MeetsBaseEligibility(Character character, OfficeDefinition definition)
        {
            if (character == null || definition == null)
                return false;

            if (!character.IsAlive || !character.IsMale)
                return false;

            if (character.Age < definition.MinAge)
                return false;

            if (definition.RequiresPlebeian && character.Class != SocialClass.Plebeian)
                return false;

            if (definition.RequiresPatrician && character.Class != SocialClass.Patrician)
                return false;

            return true;
        }

        private void EnsurePrerequisiteHistory(Character candidate, OfficeDefinition definition, int evaluationYear)
        {
            if (candidate == null || definition == null)
                return;

            if (definition.PrerequisitesAll != null)
            {
                foreach (var prerequisite in definition.PrerequisitesAll)
                {
                    SeedHistoryForRequirement(candidate, prerequisite, evaluationYear);
                }
            }

            if (definition.PrerequisitesAny != null && definition.PrerequisitesAny.Count > 0)
            {
                foreach (var option in definition.PrerequisitesAny)
                {
                    if (SeedHistoryForRequirement(candidate, option, evaluationYear))
                        break;
                }
            }
        }

        private bool SeedHistoryForRequirement(Character candidate, string officeId, int evaluationYear)
        {
            var normalized = OfficeDefinitions.NormalizeOfficeId(officeId);
            if (normalized == null)
                return false;

            if (state.HasCompletedOffice(candidate.ID, normalized) ||
                seededHistoryRecords.Contains((candidate.ID, normalized)))
            {
                return true;
            }

            var prereqDefinition = definitions.GetDefinition(normalized);
            if (prereqDefinition == null)
                return false;

            if (!MeetsBaseEligibility(candidate, prereqDefinition))
                return false;

            EnsurePrerequisiteHistory(candidate, prereqDefinition, evaluationYear - 1);

            var termWindow = ReserveHistoryWindow(candidate.ID, evaluationYear, prereqDefinition.TermLengthYears,
                prereqDefinition.ReelectionGapYears);
            state.AddHistorySeed(candidate.ID, normalized, 0, termWindow.startYear, termWindow.endYear);
            seededHistoryRecords.Add((candidate.ID, normalized));
            return true;
        }

        private (int startYear, int endYear) ReserveHistoryWindow(int characterId, int evaluationYear, int termLengthYears,
            int reelectionGapYears)
        {
            if (!historySeedCursor.TryGetValue(characterId, out var cursor))
            {
                cursor = evaluationYear - 1;
            }

            int endYear = Math.Min(cursor, evaluationYear - 1);
            int startYear = endYear - Math.Max(0, termLengthYears - 1);
            int gap = Math.Max(reelectionGapYears, 1);
            historySeedCursor[characterId] = startYear - gap;
            return (startYear, endYear);
        }

        private int EstimateInitialYear()
        {
            var living = characterSystem.GetAllLiving();
            var reference = living?.FirstOrDefault(c => c != null);
            if (reference != null)
            {
                int inferredYear = reference.BirthYear + reference.Age;
                if (inferredYear != 0)
                    return inferredYear;
            }

            return -248;
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            currentYear = e.Year;
            currentMonth = e.Month;
            currentDay = e.Day;
            if (currentMonth == 1 && currentDay == 1)
            {
                var expiredSeats = state.ExpireCompletedTerms(currentYear);
                foreach (var vacated in expiredSeats)
                {
                    LogSeatVacated(vacated);
                }
            }
            var activations = state.ActivatePendingAssignments(currentYear, definitions.GetDefinition);
            foreach (var activation in activations)
            {
                var characterName = ResolveCharacterName(activation.CharacterId);
                eventBus.Publish(new OfficeAssignedEvent(currentYear, currentMonth, currentDay,
                    activation.OfficeId, activation.Definition.Name, activation.CharacterId, activation.Seat.SeatIndex,
                    activation.StartYear, activation.EndYear, characterName));

                LogInfo($"{characterName} assumes {activation.Definition.Name} seat {activation.Seat.SeatIndex} ({FormatTermRange(activation.StartYear, activation.EndYear)}).");
            }
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            var vacated = state.RemoveCharacterFromOffices(e.CharacterID, currentYear, out var canceledAssignments);
            foreach (var seat in vacated)
            {
                LogSeatVacated(seat);
            }

            if (DebugMode)
            {
                foreach (var canceled in canceledAssignments)
                {
                    string name = ResolveCharacterName(canceled.CharacterId);
                    LogInfo($"Canceled pending assignment of {name}#{canceled.CharacterId} to {canceled.OfficeId} seat {canceled.SeatIndex}.");
                }
            }
        }

        private void LogSeatVacated(SeatVacatedInfo info)
        {
            if (info == null)
                return;

            string name = ResolveCharacterName(info.HolderId);
            string officeName = ResolveOfficeName(info.OfficeId);
            LogInfo($"{name} leaves {officeName} seat {info.SeatIndex} ({FormatTermRange(info.StartYear, info.EndYear)}).");
        }

        public OfficeDefinition GetDefinition(string officeId) => definitions.GetDefinition(officeId);

        public IReadOnlyList<OfficeDefinition> GetAllDefinitions() => definitions.GetAllDefinitions();

        public IReadOnlyList<OfficeSeatDescriptor> GetCurrentHoldings(int characterId) => state.GetCurrentHoldings(characterId);

        public bool IsEligible(Character character, OfficeDefinition definition, int year, out string reason)
        {
            return eligibility.IsEligible(character, definition, year, out reason);
        }

        public IReadOnlyList<OfficeDefinition> GetEligibleOffices(Character character, int year)
        {
            return eligibility.GetEligibleOffices(character, definitions.GetAllDefinitions(), year);
        }

        public IReadOnlyList<OfficeElectionInfo> GetElectionInfos(int year)
        {
            return state.GetElectionInfos(definitions.GetDefinition, year);
        }

        public OfficeSeatDescriptor AssignOffice(string officeId, int characterId, int year, bool deferToNextYear = true)
        {
            var result = state.AssignOffice(officeId, characterId, year, deferToNextYear, definitions.GetDefinition);

            foreach (var canceled in result.CanceledPendingAssignments)
            {
                if (DebugMode)
                {
                    string name = ResolveCharacterName(canceled.CharacterId);
                    LogInfo($"Canceled pending assignment of {name}#{canceled.CharacterId} to {canceled.OfficeId} seat {canceled.SeatIndex}.");
                }
            }

            if (result.Definition == null)
                return result.Descriptor;

            if (result.IsDeferred)
            {
                if (DebugMode)
                {
                    string name = ResolveCharacterName(result.CharacterId);
                    LogInfo($"Scheduled {name}#{result.CharacterId} to assume {result.Definition.Name} seat {result.SeatIndex} on January 1, {result.StartYear}.");
                }

                return result.Descriptor;
            }

            if (result.PreviousHolder != null)
            {
                LogSeatVacated(result.PreviousHolder);
            }

            string characterName = ResolveCharacterName(result.CharacterId);

            eventBus.Publish(new OfficeAssignedEvent(year, currentMonth, currentDay,
                result.OfficeId, result.Definition.Name, result.CharacterId, result.SeatIndex,
                result.StartYear, result.EndYear, characterName));

            LogInfo($"{characterName} assumes {result.Definition.Name} seat {result.SeatIndex} ({FormatTermRange(result.StartYear, result.EndYear)}).");

            return result.Descriptor;
        }

        public IReadOnlyList<OfficeCareerRecord> GetCareerHistory(int characterId)
        {
            return state.GetCareerHistory(characterId);
        }

        public override Dictionary<string, object> Save()
        {
            return state.Save(currentYear);
        }

        public override void Load(Dictionary<string, object> data)
        {
            var result = state.Load(data);
            if (result != null)
            {
                currentYear = result.Year;
            }

            state.EnsureSeatStructures(definitions.GetAllDefinitions());
        }

        private string ResolveCharacterName(int characterId)
        {
            var character = characterSystem?.Get(characterId);
            return character?.FullName ?? $"Character {characterId}";
        }

        private string ResolveOfficeName(string officeId)
        {
            var def = definitions.GetDefinition(officeId);
            return def?.Name ?? officeId;
        }

        private static string FormatTermRange(int startYear, int endYear)
        {
            if (startYear <= 0 && endYear <= 0)
                return "unspecified term";
            if (startYear <= 0)
                return $"term ending {endYear}";
            if (endYear <= 0)
                return $"term starting {startYear}";
            return startYear == endYear ? $"term {startYear}" : $"term {startYear}-{endYear}";
        }
    }
}
