using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using UnityEngine;

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

        private readonly Dictionary<string, OfficeDefinition> definitions = new();
        private readonly Dictionary<string, List<OfficeSeat>> seatsByOffice = new();
        private readonly Dictionary<int, List<ActiveOfficeRecord>> activeByCharacter = new();
        private readonly Dictionary<int, List<PendingOfficeRecord>> pendingByCharacter = new();
        private readonly Dictionary<int, List<OfficeCareerRecord>> historyByCharacter = new();
        private readonly Dictionary<(int characterId, string officeId), int> lastHeldYear = new();

        private readonly string dataPath;

        private int currentYear;
        private int currentMonth;
        private int currentDay;

        private bool initialHoldersSeeded;

        public bool DebugMode { get; set; }

        public OfficeSystem(EventBus.EventBus eventBus, Game.Systems.CharacterSystem.CharacterSystem characterSystem,
            string customDataPath = null)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            dataPath = string.IsNullOrWhiteSpace(customDataPath)
                ? Path.Combine("Assets", "Game", "Data", "BaseOffices.json")
                : customDataPath;
        }

        private string NormalizeOfficeId(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
                return null;

            return officeId.Trim().ToLowerInvariant();
        }

        private List<OfficeSeat> GetOrCreateSeatList(string officeId, int requiredSeats = 0)
        {
            var normalized = NormalizeOfficeId(officeId);
            if (normalized == null)
            {
                Logger.Warn("Safety", "[OfficeSystem] Requested seat list for null office id.");
                return null;
            }

            if (!seatsByOffice.TryGetValue(normalized, out var seats))
            {
                seats = new List<OfficeSeat>();
                seatsByOffice[normalized] = seats;
            }

            while (requiredSeats > 0 && seats.Count < requiredSeats)
            {
                seats.Add(new OfficeSeat { SeatIndex = seats.Count });
            }

            return seats;
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            LoadDefinitions();

            eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);

            SeedInitialOfficeHolders();
        }

        public override void Update(GameState state) { }

        private void LoadDefinitions()
        {
            if (!File.Exists(dataPath))
            {
                LogWarn($"No office definition file found at {dataPath}.");
                return;
            }

            try
            {
                var json = File.ReadAllText(dataPath);
                OfficeDefinitionCollection wrapper;
                try
                {
                    wrapper = JsonUtility.FromJson<OfficeDefinitionCollection>(json);
                }
                catch (Exception parseEx)
                {
                    LogError($"Failed to parse office definition file '{dataPath}': {parseEx.Message}");
                    return;
                }

                if (wrapper?.Offices == null)
                {
                    LogWarn("Office definition file did not contain any offices.");
                    return;
                }

                for (int i = 0; i < wrapper.Offices.Length; i++)
                {
                    var def = wrapper.Offices[i];
                    if (def == null)
                    {
                        Logger.Warn("Safety", $"{dataPath}: Office entry at index {i} was null. Skipping.");
                        continue;
                    }

                    var normalizedId = NormalizeOfficeId(def.Id);
                    if (normalizedId == null)
                    {
                        Logger.Warn("Safety", $"{dataPath}: Office entry at index {i} missing identifier.");
                        continue;
                    }

                    def.Id = normalizedId;
                    def.Name ??= def.Id;
                    def.Seats = Math.Max(1, def.Seats);
                    def.TermLengthYears = Math.Max(1, def.TermLengthYears);
                    def.ReelectionGapYears = Math.Max(0, def.ReelectionGapYears);

                    definitions[def.Id] = def;

                    var seats = GetOrCreateSeatList(def.Id, def.Seats);
                    if (seats == null)
                        continue;

                    for (int seatIndex = 0; seatIndex < seats.Count; seatIndex++)
                    {
                        if (seats[seatIndex] == null)
                            seats[seatIndex] = new OfficeSeat();

                        seats[seatIndex].SeatIndex = seatIndex;
                    }
                }

                LogInfo($"Loaded {definitions.Count} offices from data file.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to load office definitions: {ex.Message}");
            }
        }

        private void SeedInitialOfficeHolders()
        {
            if (initialHoldersSeeded)
                return;

            bool anySeatsFilled = seatsByOffice.Any(kvp => kvp.Value.Any(seat => seat.HolderId.HasValue));
            if (anySeatsFilled)
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
                var seats = GetOrCreateSeatList(officeId);
                if (seats == null || seats.Count == 0)
                    continue;

                var def = GetDefinition(officeId);
                if (def == null)
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

                    var seat = seats.Count > i ? seats[i] : null;
                    if (seat == null)
                    {
                        Logger.Warn("Safety", $"[OfficeSystem] Missing seat index {i} for office '{officeId}'.");
                        continue;
                    }
                    int termStart = initialYear - Math.Max(0, def.TermLengthYears - 1);
                    int termEnd = initialYear;

                    seat.HolderId = characterId;
                    seat.StartYear = termStart;
                    seat.EndYear = termEnd;
                    seat.PendingHolderId = null;
                    seat.PendingStartYear = 0;

                    if (!activeByCharacter.TryGetValue(characterId, out var activeList))
                    {
                        activeList = new List<ActiveOfficeRecord>();
                        activeByCharacter[characterId] = activeList;
                    }

                    activeList.RemoveAll(a => a.OfficeId == officeId && a.SeatIndex == seat.SeatIndex);
                    activeList.Add(new ActiveOfficeRecord
                    {
                        OfficeId = officeId,
                        SeatIndex = seat.SeatIndex,
                        EndYear = termEnd
                    });

                    if (DebugMode)
                    {
                        string name = ResolveCharacterName(characterId);
                        LogInfo($"Seeded {name}#{characterId} to {def.Name} seat {seat.SeatIndex} ({FormatTermRange(termStart, termEnd)}).");
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
                if (!historyByCharacter.TryGetValue(record.characterId, out var list))
                {
                    list = new List<OfficeCareerRecord>();
                    historyByCharacter[record.characterId] = list;
                }

                bool exists = list.Any(r => r.OfficeId == record.officeId && r.StartYear == record.startYear && r.EndYear == record.endYear);
                if (!exists)
                {
                    list.Add(new OfficeCareerRecord
                    {
                        OfficeId = record.officeId,
                        SeatIndex = record.seatIndex,
                        HolderId = record.characterId,
                        StartYear = record.startYear,
                        EndYear = record.endYear
                    });
                }

                lastHeldYear[(record.characterId, record.officeId)] = record.endYear;
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
                ExpireCompletedTerms();
            }
            ActivatePendingAssignments();
        }

        private void ExpireCompletedTerms()
        {
            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId.HasValue && currentYear > seat.EndYear)
                    {
                        if (!seat.PendingHolderId.HasValue || seat.PendingStartYear > currentYear)
                        {
                            // No successor has been scheduled yet; extend the current holder through the present year
                            seat.EndYear = currentYear;

                            if (activeByCharacter.TryGetValue(seat.HolderId.Value, out var activeList))
                            {
                                var activeRecord = activeList.FirstOrDefault(a =>
                                    a.OfficeId == officeId && a.SeatIndex == seat.SeatIndex);
                                if (activeRecord != null)
                                {
                                    activeRecord.EndYear = seat.EndYear;
                                }
                            }

                            continue;
                        }

                        VacateSeat(officeId, seat, seat.EndYear);
                    }
                }
            }
        }

        private void ActivatePendingAssignments()
        {
            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                var def = GetDefinition(officeId);
                if (def == null)
                    continue;

                foreach (var seat in kvp.Value)
                {
                    if (!seat.HolderId.HasValue && seat.PendingHolderId.HasValue && seat.PendingStartYear <= currentYear)
                    {
                        ActivateSeat(officeId, def, seat);
                    }
                }
            }
        }

        private void ActivateSeat(string officeId, OfficeDefinition definition, OfficeSeat seat)
        {
            if (!seat.PendingHolderId.HasValue)
                return;

            int characterId = seat.PendingHolderId.Value;

            seat.HolderId = characterId;
            seat.StartYear = currentYear;
            seat.EndYear = currentYear + definition.TermLengthYears - 1;
            seat.PendingHolderId = null;
            seat.PendingStartYear = 0;

            if (pendingByCharacter.TryGetValue(characterId, out var pendingList))
            {
                pendingList.RemoveAll(p => p.OfficeId == officeId && p.SeatIndex == seat.SeatIndex);
                if (pendingList.Count == 0)
                    pendingByCharacter.Remove(characterId);
            }

            if (!activeByCharacter.TryGetValue(characterId, out var activeList))
            {
                activeList = new List<ActiveOfficeRecord>();
                activeByCharacter[characterId] = activeList;
            }

            activeList.RemoveAll(a => a.OfficeId == officeId && a.SeatIndex == seat.SeatIndex);

            activeList.Add(new ActiveOfficeRecord
            {
                OfficeId = officeId,
                SeatIndex = seat.SeatIndex,
                EndYear = seat.EndYear
            });

            string name = ResolveCharacterName(characterId);

            eventBus.Publish(new OfficeAssignedEvent(currentYear, currentMonth, currentDay,
                officeId, definition.Name, characterId, seat.SeatIndex, seat.StartYear, seat.EndYear, name));

            LogInfo($"{name} assumes {definition.Name} seat {seat.SeatIndex} ({FormatTermRange(seat.StartYear, seat.EndYear)}).");
        }

        private void CancelPendingAssignment(string officeId, OfficeSeat seat)
        {
            if (!seat.PendingHolderId.HasValue)
                return;

            int characterId = seat.PendingHolderId.Value;

            if (pendingByCharacter.TryGetValue(characterId, out var list))
            {
                list.RemoveAll(p => p.OfficeId == officeId && p.SeatIndex == seat.SeatIndex);
                if (list.Count == 0)
                    pendingByCharacter.Remove(characterId);
            }

            if (DebugMode)
            {
                string name = ResolveCharacterName(characterId);
                LogInfo($"Canceled pending assignment of {name}#{characterId} to {officeId} seat {seat.SeatIndex}.");
            }

            seat.PendingHolderId = null;
            seat.PendingStartYear = 0;
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId == e.CharacterID)
                    {
                        VacateSeat(officeId, seat, currentYear);
                    }
                    else if (seat.PendingHolderId == e.CharacterID)
                    {
                        CancelPendingAssignment(officeId, seat);
                    }
                }
            }
        }

        private void VacateSeat(string officeId, OfficeSeat seat, int endYear)
        {
            if (!seat.HolderId.HasValue)
                return;

            int holderId = seat.HolderId.Value;
            int startYear = seat.StartYear;
            string name = ResolveCharacterName(holderId);

            string officeName = ResolveOfficeName(officeId);
            LogInfo($"{name} leaves {officeName} seat {seat.SeatIndex} ({FormatTermRange(startYear, endYear)}).");

            seat.HolderId = null;
            seat.StartYear = 0;
            seat.EndYear = 0;

            if (startYear != 0 || endYear != 0)
            {
                if (!historyByCharacter.TryGetValue(holderId, out var list))
                {
                    list = new List<OfficeCareerRecord>();
                    historyByCharacter[holderId] = list;
                }

                list.Add(new OfficeCareerRecord
                {
                    OfficeId = officeId,
                    SeatIndex = seat.SeatIndex,
                    HolderId = holderId,
                    StartYear = startYear,
                    EndYear = endYear
                });

                lastHeldYear[(holderId, officeId)] = endYear;
            }

            if (activeByCharacter.TryGetValue(holderId, out var activeList))
            {
                activeList.RemoveAll(a => a.OfficeId == officeId && a.SeatIndex == seat.SeatIndex);
                if (activeList.Count == 0)
                    activeByCharacter.Remove(holderId);
            }
        }

        public OfficeDefinition GetDefinition(string officeId)
        {
            officeId = officeId?.Trim().ToLowerInvariant();
            if (officeId != null && definitions.TryGetValue(officeId, out var def))
                return def;
            return null;
        }

        public IReadOnlyList<OfficeDefinition> GetAllDefinitions() => definitions.Values.ToList();

        public IReadOnlyList<OfficeSeatDescriptor> GetCurrentHoldings(int characterId)
        {
            var result = new List<OfficeSeatDescriptor>();
            foreach (var kvp in seatsByOffice)
            {
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId == characterId)
                    {
                        result.Add(new OfficeSeatDescriptor
                        {
                            OfficeId = kvp.Key,
                            SeatIndex = seat.SeatIndex,
                            HolderId = seat.HolderId,
                            StartYear = seat.StartYear,
                            EndYear = seat.EndYear,
                            PendingHolderId = seat.PendingHolderId,
                            PendingStartYear = seat.PendingStartYear
                        });
                    }
                }
            }
            return result;
        }

        public bool IsEligible(Character character, OfficeDefinition definition, int year, out string reason)
        {
            reason = null;

            if (character == null)
            {
                reason = "No character";
                return false;
            }

            if (definition == null)
            {
                reason = "No office";
                return false;
            }

            if (!character.IsAlive)
            {
                reason = "Deceased";
                return false;
            }

            if (!character.IsMale)
            {
                reason = "Office restricted to men";
                return false;
            }

            if (character.Age < definition.MinAge)
            {
                reason = "Too young";
                return false;
            }

            if (definition.RequiresPlebeian && character.Class != SocialClass.Plebeian)
            {
                reason = "Requires plebeian status";
                return false;
            }

            if (definition.RequiresPatrician && character.Class != SocialClass.Patrician)
            {
                reason = "Requires patrician status";
                return false;
            }

            if (definition.PrerequisitesAll != null)
            {
                foreach (var prereq in definition.PrerequisitesAll)
                {
                    if (string.IsNullOrWhiteSpace(prereq))
                        continue;

                    if (!HasQualifiedForOffice(character.ID, prereq, year))
                    {
                        reason = $"Requires prior service as {prereq}";
                        return false;
                    }
                }
            }

            if (definition.PrerequisitesAny != null && definition.PrerequisitesAny.Count > 0)
            {
                bool satisfied = definition.PrerequisitesAny.Any(p => HasQualifiedForOffice(character.ID, p, year));
                if (!satisfied)
                {
                    reason = "Prerequisite offices not held";
                    return false;
                }
            }

            if (lastHeldYear.TryGetValue((character.ID, definition.Id), out var lastYear))
            {
                int diff = year - lastYear;
                if (diff < definition.ReelectionGapYears)
                {
                    reason = $"Must wait {definition.ReelectionGapYears - diff} more years";
                    return false;
                }
            }

            if (activeByCharacter.TryGetValue(character.ID, out var activeList))
            {
                foreach (var active in activeList)
                {
                    if (active.OfficeId == definition.Id && active.EndYear >= year)
                    {
                        reason = "Currently holds this office";
                        return false;
                    }

                    if (active.EndYear > year)
                    {
                        reason = "Currently serving another magistracy";
                        return false;
                    }
                }
            }

            if (pendingByCharacter.TryGetValue(character.ID, out var pendingList))
            {
                foreach (var pending in pendingList)
                {
                    if (pending.OfficeId == definition.Id)
                    {
                        reason = "Already elected to this office";
                        return false;
                    }

                    if (pending.StartYear >= year)
                    {
                        reason = "Awaiting another magistracy";
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HasCompletedOffice(int characterId, string officeId)
        {
            officeId = officeId?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(officeId))
                return false;

            if (historyByCharacter.TryGetValue(characterId, out var records))
            {
                return records.Any(r => r.OfficeId == officeId);
            }

            return false;
        }

        private bool HasQualifiedForOffice(int characterId, string officeId, int electionYear)
        {
            officeId = officeId?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(officeId))
                return false;

            if (HasCompletedOffice(characterId, officeId))
                return true;

            if (activeByCharacter.TryGetValue(characterId, out var activeList))
            {
                foreach (var active in activeList)
                {
                    if (active.OfficeId == officeId && active.EndYear <= electionYear)
                        return true;
                }
            }

            return false;
        }

        public IReadOnlyList<OfficeDefinition> GetEligibleOffices(Character character, int year)
        {
            var result = new List<OfficeDefinition>();
            foreach (var def in definitions.Values)
            {
                if (IsEligible(character, def, year, out _))
                    result.Add(def);
            }

            return result.OrderBy(d => d.Rank).ThenBy(d => d.MinAge).ToList();
        }

        public IReadOnlyList<OfficeElectionInfo> GetElectionInfos(int year)
        {
            var elections = new List<OfficeElectionInfo>();

            foreach (var kvp in seatsByOffice)
            {
                var def = GetDefinition(kvp.Key);
                if (def == null)
                    continue;

                var descriptors = new List<OfficeSeatDescriptor>();
                foreach (var seat in kvp.Value)
                {
                    if (seat == null)
                        continue;

                    bool hasPending = seat.PendingHolderId.HasValue;
                    bool isVacant = !seat.HolderId.HasValue && !hasPending;
                    bool expiring = seat.HolderId.HasValue && seat.EndYear <= year && !hasPending;
                    if (!isVacant && !expiring)
                        continue;

                    descriptors.Add(new OfficeSeatDescriptor
                    {
                        OfficeId = kvp.Key,
                        SeatIndex = seat.SeatIndex,
                        HolderId = seat.HolderId,
                        StartYear = seat.StartYear,
                        EndYear = seat.EndYear,
                        PendingHolderId = seat.PendingHolderId,
                        PendingStartYear = seat.PendingStartYear
                    });
                }

                if (descriptors.Count == 0)
                    continue;

                elections.Add(new OfficeElectionInfo
                {
                    Definition = def,
                    SeatsAvailable = descriptors.Count,
                    Seats = descriptors
                });
            }

            return elections
                .OrderByDescending(e => e.Definition.Rank)
                .ThenBy(e => e.Definition.MinAge)
                .ToList();
        }

        public OfficeSeatDescriptor AssignOffice(string officeId, int characterId, int year, bool deferToNextYear = true)
        {
            var normalizedId = NormalizeOfficeId(officeId);
            if (normalizedId == null)
            {
                Logger.Warn("Safety", "[OfficeSystem] AssignOffice called with null identifier.");
                return new OfficeSeatDescriptor { OfficeId = officeId ?? "unknown", SeatIndex = -1, StartYear = year, EndYear = year };
            }

            if (!definitions.TryGetValue(normalizedId, out var def))
            {
                Logger.Warn("Safety", $"[OfficeSystem] AssignOffice called with unknown office '{normalizedId}'.");
                return new OfficeSeatDescriptor { OfficeId = normalizedId, SeatIndex = -1, StartYear = year, EndYear = year };
            }

            var seats = GetOrCreateSeatList(normalizedId, def.Seats);
            if (seats == null)
            {
                Logger.Warn("Safety", $"[OfficeSystem] No seats collection available for '{normalizedId}'.");
                return new OfficeSeatDescriptor { OfficeId = normalizedId, SeatIndex = -1, StartYear = year, EndYear = year };
            }

            OfficeSeat seat = null;

            if (deferToNextYear)
            {
                seat = seats.FirstOrDefault(s => s != null && s.HolderId.HasValue && s.EndYear <= year && !s.PendingHolderId.HasValue)
                    ?? seats.FirstOrDefault(s => s != null && !s.HolderId.HasValue && !s.PendingHolderId.HasValue)
                    ?? seats.FirstOrDefault(s => s != null && !s.PendingHolderId.HasValue);
            }
            else
            {
                seat = seats.FirstOrDefault(s => s != null && !s.HolderId.HasValue)
                    ?? seats.FirstOrDefault(s => s != null && s.HolderId.HasValue && s.EndYear <= year);
            }

            if (seat == null)
            {
                seat = new OfficeSeat { SeatIndex = seats.Count };
                seats.Add(seat);
            }

            if (deferToNextYear)
            {
                CancelPendingAssignment(normalizedId, seat);

                int startYear = year + 1;

                seat.PendingHolderId = characterId;
                seat.PendingStartYear = startYear;

                if (!pendingByCharacter.TryGetValue(characterId, out var pendingList))
                {
                    pendingList = new List<PendingOfficeRecord>();
                    pendingByCharacter[characterId] = pendingList;
                }

                pendingList.RemoveAll(p => p.OfficeId == normalizedId && p.SeatIndex == seat.SeatIndex);
                pendingList.Add(new PendingOfficeRecord
                {
                    OfficeId = normalizedId,
                    SeatIndex = seat.SeatIndex,
                    StartYear = startYear
                });

                if (DebugMode)
                {
                    string name = ResolveCharacterName(characterId);
                    LogInfo($"Scheduled {name}#{characterId} to assume {def.Name} seat {seat.SeatIndex} on January 1, {startYear}.");
                }

                return new OfficeSeatDescriptor
                {
                    OfficeId = normalizedId,
                    SeatIndex = seat.SeatIndex,
                    HolderId = characterId,
                    StartYear = startYear,
                    EndYear = startYear + def.TermLengthYears - 1,
                    PendingHolderId = characterId,
                    PendingStartYear = startYear
                };
            }

            if (seat.HolderId.HasValue)
                VacateSeat(normalizedId, seat, seat.EndYear);

            CancelPendingAssignment(normalizedId, seat);

            seat.HolderId = characterId;
            seat.StartYear = year;
            seat.EndYear = year + def.TermLengthYears - 1;

            if (!activeByCharacter.TryGetValue(characterId, out var activeList))
            {
                activeList = new List<ActiveOfficeRecord>();
                activeByCharacter[characterId] = activeList;
            }

            activeList.RemoveAll(a => a.OfficeId == normalizedId && a.SeatIndex == seat.SeatIndex);
            activeList.Add(new ActiveOfficeRecord
            {
                OfficeId = normalizedId,
                SeatIndex = seat.SeatIndex,
                EndYear = seat.EndYear
            });

            string characterName = ResolveCharacterName(characterId);

            eventBus.Publish(new OfficeAssignedEvent(year, currentMonth, currentDay,
                normalizedId, def.Name, characterId, seat.SeatIndex, seat.StartYear, seat.EndYear, characterName));

            LogInfo($"{characterName} assumes {def.Name} seat {seat.SeatIndex} ({FormatTermRange(seat.StartYear, seat.EndYear)}).");

            return new OfficeSeatDescriptor
            {
                OfficeId = normalizedId,
                SeatIndex = seat.SeatIndex,
                HolderId = characterId,
                StartYear = seat.StartYear,
                EndYear = seat.EndYear,
                PendingHolderId = seat.PendingHolderId,
                PendingStartYear = seat.PendingStartYear
            };
        }

        private string ResolveCharacterName(int characterId)
        {
            var character = characterSystem?.Get(characterId);
            return character?.FullName ?? $"Character {characterId}";
        }

        private string ResolveOfficeName(string officeId)
        {
            var def = GetDefinition(officeId);
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

        public IReadOnlyList<OfficeCareerRecord> GetCareerHistory(int characterId)
        {
            if (historyByCharacter.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<OfficeCareerRecord>();
        }

        public override Dictionary<string, object> Save()
        {
            var blob = new OfficeSaveBlob
            {
                Year = currentYear,
                Seats = seatsByOffice.Select(kvp => new OfficeSeatSave
                {
                    OfficeId = kvp.Key,
                    Seats = kvp.Value.Select(seat => new SeatSaveData
                    {
                        SeatIndex = seat.SeatIndex,
                        HolderId = seat.HolderId,
                        StartYear = seat.StartYear,
                        EndYear = seat.EndYear,
                        PendingHolderId = seat.PendingHolderId,
                        PendingStartYear = seat.PendingStartYear
                    }).ToList()
                }).ToList(),
                LastHeld = lastHeldYear.Select(entry => new LastHeldEntry
                {
                    CharacterId = entry.Key.characterId,
                    OfficeId = entry.Key.officeId,
                    Year = entry.Value
                }).ToList(),
                History = historyByCharacter.Select(kvp => new HistoryEntry
                {
                    CharacterId = kvp.Key,
                    Records = kvp.Value.Select(record => new CareerSaveData
                    {
                        OfficeId = record.OfficeId,
                        SeatIndex = record.SeatIndex,
                        HolderId = record.HolderId,
                        StartYear = record.StartYear,
                        EndYear = record.EndYear
                    }).ToList()
                }).ToList()
            };

            string json = JsonUtility.ToJson(blob);
            return new Dictionary<string, object> { ["json"] = json };
        }

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null || !data.TryGetValue("json", out var raw) || raw is not string json)
                return;

            try
            {
                var blob = JsonUtility.FromJson<OfficeSaveBlob>(json);
                if (blob == null)
                    return;

                currentYear = blob.Year;

                activeByCharacter.Clear();
                pendingByCharacter.Clear();
                if (blob.Seats != null)
                {
                    foreach (var seatEntry in blob.Seats)
                    {
                        var normalizedId = NormalizeOfficeId(seatEntry.OfficeId);
                        var seats = GetOrCreateSeatList(normalizedId);
                        if (seats == null)
                            continue;

                        seats.Clear();
                        if (seatEntry.Seats == null) continue;

                        foreach (var entry in seatEntry.Seats)
                        {
                            var seat = new OfficeSeat
                            {
                                SeatIndex = entry.SeatIndex,
                                HolderId = entry.HolderId,
                                StartYear = entry.StartYear,
                                EndYear = entry.EndYear,
                                PendingHolderId = entry.PendingHolderId,
                                PendingStartYear = entry.PendingStartYear
                            };

                            seats.Add(seat);

                            if (entry.HolderId.HasValue)
                            {
                                if (!activeByCharacter.TryGetValue(entry.HolderId.Value, out var activeList))
                                {
                                    activeList = new List<ActiveOfficeRecord>();
                                    activeByCharacter[entry.HolderId.Value] = activeList;
                                }

                                activeList.Add(new ActiveOfficeRecord
                                {
                                    OfficeId = normalizedId,
                                    SeatIndex = entry.SeatIndex,
                                    EndYear = entry.EndYear
                                });
                            }

                            if (entry.PendingHolderId.HasValue)
                            {
                                if (!pendingByCharacter.TryGetValue(entry.PendingHolderId.Value, out var pendingList))
                                {
                                    pendingList = new List<PendingOfficeRecord>();
                                    pendingByCharacter[entry.PendingHolderId.Value] = pendingList;
                                }

                                pendingList.Add(new PendingOfficeRecord
                                {
                                    OfficeId = normalizedId,
                                    SeatIndex = entry.SeatIndex,
                                    StartYear = entry.PendingStartYear
                                });
                            }
                        }
                    }
                }

                lastHeldYear.Clear();
                if (blob.LastHeld != null)
                {
                    foreach (var entry in blob.LastHeld)
                    {
                        lastHeldYear[(entry.CharacterId, entry.OfficeId)] = entry.Year;
                    }
                }

                historyByCharacter.Clear();
                if (blob.History != null)
                {
                    foreach (var entry in blob.History)
                    {
                        var list = new List<OfficeCareerRecord>();
                        foreach (var record in entry.Records)
                        {
                            list.Add(new OfficeCareerRecord
                            {
                                OfficeId = record.OfficeId,
                                SeatIndex = record.SeatIndex,
                                HolderId = record.HolderId,
                                StartYear = record.StartYear,
                                EndYear = record.EndYear
                            });
                        }
                        historyByCharacter[entry.CharacterId] = list;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load office save data: {ex.Message}");
            }
        }

        [Serializable]
        private class OfficeSaveBlob
        {
            public int Year;
            public List<OfficeSeatSave> Seats = new();
            public List<LastHeldEntry> LastHeld = new();
            public List<HistoryEntry> History = new();
        }

        [Serializable]
        private class SeatSaveData
        {
            public int SeatIndex;
            public int? HolderId;
            public int StartYear;
            public int EndYear;
            public int? PendingHolderId;
            public int PendingStartYear;
        }

        [Serializable]
        private class OfficeSeatSave
        {
            public string OfficeId;
            public List<SeatSaveData> Seats = new();
        }

        [Serializable]
        private class LastHeldEntry
        {
            public int CharacterId;
            public string OfficeId;
            public int Year;
        }

        [Serializable]
        private class CareerSaveData
        {
            public string OfficeId;
            public int SeatIndex;
            public int HolderId;
            public int StartYear;
            public int EndYear;
        }

        [Serializable]
        private class HistoryEntry
        {
            public int CharacterId;
            public List<CareerSaveData> Records = new();
        }
    }
}
