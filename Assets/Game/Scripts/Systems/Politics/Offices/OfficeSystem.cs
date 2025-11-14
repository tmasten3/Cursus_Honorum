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

            var assignments = new Dictionary<string, int[]>
            {
                ["consul"] = new[] { 283, 269 },
                ["censor"] = new[] { 273, 261 },
                ["praetor"] = new[] { 255, 279 },
                ["aedile"] = new[] { 25, 31 },
                ["plebeian_aedile"] = new[] { 37, 43 },
                ["tribune"] = new[] { 97, 103, 253, 259, 271 },
                ["quaestor"] = new[] { 1, 7, 13, 19, 49, 55, 79, 121 }
            };

            foreach (var kvp in assignments)
            {
                string officeId = kvp.Key;
                var def = definitions.GetDefinition(officeId);
                if (def == null)
                    continue;

                var seats = this.state.GetOrCreateSeatList(officeId, def.Seats);
                if (seats == null || seats.Count == 0)
                    continue;

                var holderIds = kvp.Value;
                int seatCount = Math.Min(holderIds.Length, seats.Count);
                for (int i = 0; i < seatCount; i++)
                {
                    int characterId = holderIds[i];
                    var character = characterSystem.Get(characterId);
                    if (character == null || !character.IsAlive)
                    {
                        LogWarn($"Unable to seed {officeId} seat {seats[i].SeatIndex}: character {characterId} missing or deceased.");
                        continue;
                    }

                    int termStart = initialYear - Math.Max(0, def.TermLengthYears - 1);
                    int termEnd = initialYear;

                    bool assigned = this.state.TryAssignInitialHolder(officeId, seats[i].SeatIndex, characterId, termStart, termEnd);
                    if (!assigned)
                        continue;

                    if (DebugMode)
                    {
                        string name = ResolveCharacterName(characterId);
                        LogInfo($"Seeded {name}#{characterId} to {def.Name} seat {seats[i].SeatIndex} ({FormatTermRange(termStart, termEnd)}).");
                    }
                }
            }

            var historySeeds = new (int characterId, string officeId, int seatIndex, int startYear, int endYear)[]
            {
                (283, "praetor", 0, initialYear - 2, initialYear - 1),
                (269, "praetor", 1, initialYear - 2, initialYear - 1),
                (273, "consul", 0, initialYear - 3, initialYear - 3),
                (261, "consul", 1, initialYear - 3, initialYear - 3),
                (25, "quaestor", 0, initialYear - 3, initialYear - 2),
                (31, "quaestor", 1, initialYear - 3, initialYear - 2),
                (255, "quaestor", 2, initialYear - 4, initialYear - 3),
                (279, "quaestor", 3, initialYear - 4, initialYear - 3)
            };

            foreach (var record in historySeeds)
            {
                this.state.AddHistorySeed(record.characterId, record.officeId, record.seatIndex, record.startYear, record.endYear);
            }

            initialHoldersSeeded = true;
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
