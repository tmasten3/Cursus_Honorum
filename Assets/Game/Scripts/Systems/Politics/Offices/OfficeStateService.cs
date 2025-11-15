using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Systems.Politics.Offices
{
    public class OfficeStateService
    {
        private readonly Dictionary<string, List<OfficeSeat>> seatsByOffice = new Dictionary<string, List<OfficeSeat>>();
        private readonly Dictionary<int, List<ActiveOfficeRecord>> activeByCharacter = new Dictionary<int, List<ActiveOfficeRecord>>();
        private readonly Dictionary<int, List<PendingOfficeRecord>> pendingByCharacter = new Dictionary<int, List<PendingOfficeRecord>>();
        private readonly Dictionary<int, List<OfficeCareerRecord>> historyByCharacter = new Dictionary<int, List<OfficeCareerRecord>>();
        private readonly Dictionary<(int characterId, string officeId), int> lastHeldYear = new Dictionary<(int characterId, string officeId), int>();

        private readonly Action<string> logWarn;

        public OfficeStateService(Action<string> logWarn)
        {
            this.logWarn = logWarn;
        }

        public List<OfficeSeat> GetOrCreateSeatList(string officeId, int requiredSeats = 0)
        {
            var normalized = OfficeDefinitions.NormalizeOfficeId(officeId);
            if (normalized == null)
            {
                Game.Core.Logger.Warn("Safety", "[OfficeState] Requested seat list for null office id.");
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

        public void EnsureSeatStructures(IEnumerable<OfficeDefinition> definitions)
        {
            if (definitions == null)
                return;

            foreach (var def in definitions)
            {
                var seats = GetOrCreateSeatList(def?.Id, def?.Seats ?? 0);
                if (seats == null)
                    continue;

                for (int seatIndex = 0; seatIndex < seats.Count; seatIndex++)
                {
                    if (seats[seatIndex] == null)
                        seats[seatIndex] = new OfficeSeat();

                    seats[seatIndex].SeatIndex = seatIndex;
                }
            }
        }

        public bool AnySeatsFilled()
        {
            return seatsByOffice.Any(kvp => kvp.Value.Any(seat => seat.HolderId.HasValue));
        }

        public bool TryAssignInitialHolder(string officeId, int seatIndex, int characterId, int termStart, int termEnd)
        {
            var normalizedId = OfficeDefinitions.NormalizeOfficeId(officeId);
            var seats = GetOrCreateSeatList(normalizedId);
            if (seats == null || seatIndex < 0)
                return false;

            var seat = seats.Count > seatIndex ? seats[seatIndex] : null;
            if (seat == null)
            {
                Game.Core.Logger.Warn("Safety", $"[OfficeState] Missing seat index {seatIndex} for office '{normalizedId}'.");
                return false;
            }

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

            activeList.RemoveAll(a => a.OfficeId == normalizedId && a.SeatIndex == seat.SeatIndex);
            activeList.Add(new ActiveOfficeRecord
            {
                OfficeId = normalizedId,
                SeatIndex = seat.SeatIndex,
                EndYear = termEnd
            });

            return true;
        }

        public void AddHistorySeed(int characterId, string officeId, int seatIndex, int startYear, int endYear)
        {
            var normalizedId = OfficeDefinitions.NormalizeOfficeId(officeId);

            if (!historyByCharacter.TryGetValue(characterId, out var list))
            {
                list = new List<OfficeCareerRecord>();
                historyByCharacter[characterId] = list;
            }

            bool exists = list.Any(r => r.OfficeId == normalizedId && r.StartYear == startYear && r.EndYear == endYear);
            if (!exists)
            {
                list.Add(new OfficeCareerRecord
                {
                    OfficeId = normalizedId,
                    SeatIndex = seatIndex,
                    HolderId = characterId,
                    StartYear = startYear,
                    EndYear = endYear
                });
            }

            lastHeldYear[(characterId, normalizedId)] = endYear;
        }

        public IReadOnlyList<SeatVacatedInfo> ExpireCompletedTerms(int currentYear)
        {
            var vacated = new List<SeatVacatedInfo>();

            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId.HasValue && currentYear > seat.EndYear)
                    {
                        if (!seat.PendingHolderId.HasValue || seat.PendingStartYear > currentYear)
                        {
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

                        var vacatedInfo = VacateSeatInternal(officeId, seat, seat.EndYear);
                        if (vacatedInfo != null)
                            vacated.Add(vacatedInfo);
                    }
                }
            }

            return vacated;
        }

        public IReadOnlyList<SeatActivationInfo> ActivatePendingAssignments(int currentYear, Func<string, OfficeDefinition> getDefinition)
        {
            var activations = new List<SeatActivationInfo>();
            if (getDefinition == null)
                return activations;

            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                var definition = getDefinition(officeId);
                if (definition == null)
                    continue;

                foreach (var seat in kvp.Value)
                {
                    if (!seat.HolderId.HasValue && seat.PendingHolderId.HasValue && seat.PendingStartYear <= currentYear)
                    {
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

                        activations.Add(new SeatActivationInfo
                        {
                            OfficeId = officeId,
                            Definition = definition,
                            Seat = seat,
                            CharacterId = characterId,
                            StartYear = seat.StartYear,
                            EndYear = seat.EndYear
                        });
                    }
                }
            }

            return activations;
        }

        public PendingAssignmentCanceledInfo CancelPendingAssignment(string officeId, OfficeSeat seat)
        {
            if (!seat.PendingHolderId.HasValue)
                return null;

            int characterId = seat.PendingHolderId.Value;

            if (pendingByCharacter.TryGetValue(characterId, out var list))
            {
                list.RemoveAll(p => p.OfficeId == officeId && p.SeatIndex == seat.SeatIndex);
                if (list.Count == 0)
                    pendingByCharacter.Remove(characterId);
            }

            seat.PendingHolderId = null;
            seat.PendingStartYear = 0;

            return new PendingAssignmentCanceledInfo
            {
                OfficeId = officeId,
                SeatIndex = seat.SeatIndex,
                CharacterId = characterId
            };
        }

        public IReadOnlyList<SeatVacatedInfo> RemoveCharacterFromOffices(int characterId, int currentYear, out List<PendingAssignmentCanceledInfo> canceledAssignments)
        {
            var vacatedSeats = new List<SeatVacatedInfo>();
            canceledAssignments = new List<PendingAssignmentCanceledInfo>();

            foreach (var kvp in seatsByOffice)
            {
                string officeId = kvp.Key;
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId == characterId)
                    {
                        var vacated = VacateSeatInternal(officeId, seat, currentYear);
                        if (vacated != null)
                            vacatedSeats.Add(vacated);
                    }
                    else if (seat.PendingHolderId == characterId)
                    {
                        var canceled = CancelPendingAssignment(officeId, seat);
                        if (canceled != null)
                            canceledAssignments.Add(canceled);
                    }
                }
            }

            return vacatedSeats;
        }

        public OfficeAssignmentResult AssignOffice(string officeId, int characterId, int year, bool deferToNextYear, Func<string, OfficeDefinition> getDefinition)
        {
            var normalizedId = OfficeDefinitions.NormalizeOfficeId(officeId);
            if (normalizedId == null)
            {
                Game.Core.Logger.Warn("Safety", "[OfficeState] AssignOffice called with null identifier.");
                return OfficeAssignmentResult.Error(officeId ?? "unknown", year);
            }

            var definition = getDefinition?.Invoke(normalizedId);
            if (definition == null)
            {
                Game.Core.Logger.Warn("Safety", $"[OfficeState] AssignOffice called with unknown office '{normalizedId}'.");
                return OfficeAssignmentResult.Error(normalizedId, year);
            }

            var seats = GetOrCreateSeatList(normalizedId, definition.Seats);
            if (seats == null)
            {
                Game.Core.Logger.Warn("Safety", $"[OfficeState] No seats collection available for '{normalizedId}'.");
                return OfficeAssignmentResult.Error(normalizedId, year);
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

            var result = new OfficeAssignmentResult(normalizedId, definition, seat.SeatIndex, characterId);

            if (deferToNextYear)
            {
                var canceled = CancelPendingAssignment(normalizedId, seat);
                if (canceled != null)
                    result.CanceledPendingAssignments.Add(canceled);

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

                result.MarkDeferred(startYear, startYear + definition.TermLengthYears - 1, seat);
                return result;
            }

            if (seat.HolderId.HasValue)
            {
                var vacated = VacateSeatInternal(normalizedId, seat, seat.EndYear);
                if (vacated != null)
                    result.PreviousHolder = vacated;
            }

            var canceledPending = CancelPendingAssignment(normalizedId, seat);
            if (canceledPending != null)
                result.CanceledPendingAssignments.Add(canceledPending);

            seat.HolderId = characterId;
            seat.StartYear = year;
            seat.EndYear = year + definition.TermLengthYears - 1;

            if (!activeByCharacter.TryGetValue(characterId, out var activeRecords))
            {
                activeRecords = new List<ActiveOfficeRecord>();
                activeByCharacter[characterId] = activeRecords;
            }

            activeRecords.RemoveAll(a => a.OfficeId == normalizedId && a.SeatIndex == seat.SeatIndex);
            activeRecords.Add(new ActiveOfficeRecord
            {
                OfficeId = normalizedId,
                SeatIndex = seat.SeatIndex,
                EndYear = seat.EndYear
            });

            result.MarkImmediate(year, seat.EndYear, seat);
            return result;
        }

        public IReadOnlyList<OfficeSeatDescriptor> GetCurrentHoldings(int characterId)
        {
            var result = new List<OfficeSeatDescriptor>();
            foreach (var kvp in seatsByOffice)
            {
                foreach (var seat in kvp.Value)
                {
                    if (seat.HolderId == characterId || seat.PendingHolderId == characterId)
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

        public IReadOnlyList<OfficeElectionInfo> GetElectionInfos(Func<string, OfficeDefinition> getDefinition, int year)
        {
            var elections = new List<OfficeElectionInfo>();
            if (getDefinition == null)
                return elections;

            foreach (var kvp in seatsByOffice)
            {
                var def = getDefinition(kvp.Key);
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

        public IReadOnlyList<OfficeCareerRecord> GetCareerHistory(int characterId)
        {
            if (historyByCharacter.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<OfficeCareerRecord>();
        }

        public bool TryGetLastHeldYear(int characterId, string officeId, out int year)
        {
            return lastHeldYear.TryGetValue((characterId, officeId), out year);
        }

        public IEnumerable<ActiveOfficeRecord> GetActiveRecords(int characterId)
        {
            if (activeByCharacter.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<ActiveOfficeRecord>();
        }

        public IEnumerable<PendingOfficeRecord> GetPendingRecords(int characterId)
        {
            if (pendingByCharacter.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<PendingOfficeRecord>();
        }

        public bool HasCompletedOffice(int characterId, string officeId)
        {
            if (historyByCharacter.TryGetValue(characterId, out var records))
            {
                return records.Any(r => r.OfficeId == officeId);
            }

            return false;
        }

        public Dictionary<string, object> Save(int currentYear)
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

            string json = UnityEngine.JsonUtility.ToJson(blob);
            return new Dictionary<string, object> { ["json"] = json };
        }

        public OfficeStateLoadResult Load(Dictionary<string, object> data)
        {
            var result = new OfficeStateLoadResult();
            if (data == null || !data.TryGetValue("json", out var raw) || raw is not string json)
                return result;

            try
            {
                var blob = UnityEngine.JsonUtility.FromJson<OfficeSaveBlob>(json);
                if (blob == null)
                    return result;

                result.Year = blob.Year;

                activeByCharacter.Clear();
                pendingByCharacter.Clear();
                seatsByOffice.Clear();

                if (blob.Seats != null)
                {
                    foreach (var seatEntry in blob.Seats)
                    {
                        var normalizedId = OfficeDefinitions.NormalizeOfficeId(seatEntry.OfficeId);
                        var seats = GetOrCreateSeatList(normalizedId);
                        if (seats == null)
                            continue;

                        seats.Clear();
                        if (seatEntry.Seats == null)
                            continue;

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
                        var normalizedId = OfficeDefinitions.NormalizeOfficeId(entry.OfficeId);
                        if (normalizedId == null)
                        {
                            logWarn?.Invoke($"Last-held entry for character {entry.CharacterId} had an invalid office id '{entry.OfficeId}'.");
                            continue;
                        }

                        lastHeldYear[(entry.CharacterId, normalizedId)] = entry.Year;
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
                logWarn?.Invoke($"Failed to load office save data: {ex.Message}");
            }

            return result;
        }

        private SeatVacatedInfo VacateSeatInternal(string officeId, OfficeSeat seat, int endYear)
        {
            if (!seat.HolderId.HasValue)
                return null;

            int holderId = seat.HolderId.Value;
            int startYear = seat.StartYear;

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

            return new SeatVacatedInfo
            {
                OfficeId = officeId,
                SeatIndex = seat.SeatIndex,
                HolderId = holderId,
                StartYear = startYear,
                EndYear = endYear
            };
        }

        [Serializable]
        private class OfficeSaveBlob
        {
            public int Year;
            public List<OfficeSeatSave> Seats;
            public List<LastHeldEntry> LastHeld;
            public List<HistoryEntry> History;
        }

        [Serializable]
        private class OfficeSeatSave
        {
            public string OfficeId;
            public List<SeatSaveData> Seats;
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
        private class LastHeldEntry
        {
            public int CharacterId;
            public string OfficeId;
            public int Year;
        }

        [Serializable]
        private class HistoryEntry
        {
            public int CharacterId;
            public List<CareerSaveData> Records;
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
    }

    public class SeatActivationInfo
    {
        public string OfficeId { get; set; }
        public OfficeDefinition Definition { get; set; }
        public OfficeSeat Seat { get; set; }
        public int CharacterId { get; set; }
        public int StartYear { get; set; }
        public int EndYear { get; set; }
    }

    public class SeatVacatedInfo
    {
        public string OfficeId { get; set; }
        public int SeatIndex { get; set; }
        public int HolderId { get; set; }
        public int StartYear { get; set; }
        public int EndYear { get; set; }
    }

    public class PendingAssignmentCanceledInfo
    {
        public string OfficeId { get; set; }
        public int SeatIndex { get; set; }
        public int CharacterId { get; set; }
    }

    public class OfficeAssignmentResult
    {
        public OfficeAssignmentResult(string officeId, OfficeDefinition definition, int seatIndex, int characterId)
        {
            OfficeId = officeId;
            Definition = definition;
            SeatIndex = seatIndex;
            CharacterId = characterId;
        }

        public string OfficeId { get; }
        public OfficeDefinition Definition { get; }
        public int SeatIndex { get; }
        public int CharacterId { get; }
        public bool IsDeferred { get; private set; }
        public int StartYear { get; private set; }
        public int EndYear { get; private set; }
        public OfficeSeatDescriptor Descriptor { get; private set; }
        public SeatVacatedInfo PreviousHolder { get; set; }
        public List<PendingAssignmentCanceledInfo> CanceledPendingAssignments { get; } = new List<PendingAssignmentCanceledInfo>();

        public static OfficeAssignmentResult Error(string officeId, int year)
        {
            var result = new OfficeAssignmentResult(officeId, null, -1, -1)
            {
                StartYear = year,
                EndYear = year
            };

            result.Descriptor = new OfficeSeatDescriptor
            {
                OfficeId = officeId,
                SeatIndex = -1,
                StartYear = year,
                EndYear = year
            };

            return result;
        }

        public void MarkDeferred(int startYear, int endYear, OfficeSeat seat)
        {
            IsDeferred = true;
            StartYear = startYear;
            EndYear = endYear;
            Descriptor = new OfficeSeatDescriptor
            {
                OfficeId = OfficeId,
                SeatIndex = SeatIndex,
                HolderId = CharacterId,
                StartYear = startYear,
                EndYear = endYear,
                PendingHolderId = CharacterId,
                PendingStartYear = startYear
            };
        }

        public void MarkImmediate(int startYear, int endYear, OfficeSeat seat)
        {
            IsDeferred = false;
            StartYear = startYear;
            EndYear = endYear;
            Descriptor = new OfficeSeatDescriptor
            {
                OfficeId = OfficeId,
                SeatIndex = SeatIndex,
                HolderId = CharacterId,
                StartYear = startYear,
                EndYear = endYear,
                PendingHolderId = seat.PendingHolderId,
                PendingStartYear = seat.PendingStartYear
            };
        }
    }

    public class OfficeStateLoadResult
    {
        public int Year { get; set; }
    }
}
