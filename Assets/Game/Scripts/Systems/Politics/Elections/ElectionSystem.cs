using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using UnityEngine;

namespace Game.Systems.Politics.Elections
{
    public class ElectionSystem : GameSystemBase
    {
        public override string Name => "Election System";
        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(EventBus.EventBus),
            typeof(TimeSystem),
            typeof(Game.Systems.CharacterSystem.CharacterSystem),
            typeof(OfficeSystem)
        };

        private readonly EventBus.EventBus eventBus;
        private readonly TimeSystem timeSystem;
        private readonly Game.Systems.CharacterSystem.CharacterSystem characterSystem;
        private readonly OfficeSystem officeSystem;

        private const int DefaultRngSeed = 15821;

        private readonly ElectionCalendar calendar;
        private readonly CandidateEvaluationService candidateEvaluation;
        private readonly ElectionVoteSimulator voteSimulator;
        private readonly ElectionResultService resultsApplier;

        private readonly Dictionary<string, List<CandidateDeclaration>> declarationsByOffice = new();
        private readonly Dictionary<int, List<CandidateDeclaration>> declarationsByYear = new();
        private readonly Dictionary<int, List<ElectionResultRecord>> resultsByYear = new();
        private readonly Dictionary<int, CandidateDeclaration> declarationByCharacter = new();

        private readonly TrackingRandom rng;
        private int rngSeed = DefaultRngSeed;

        private int currentYear;
        private int currentMonth;
        private int currentDay;

        public bool DebugMode { get; set; }

        public ElectionSystem(EventBus.EventBus eventBus, TimeSystem timeSystem,
            Game.Systems.CharacterSystem.CharacterSystem characterSystem, OfficeSystem officeSystem)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            this.officeSystem = officeSystem ?? throw new ArgumentNullException(nameof(officeSystem));

            rng = new TrackingRandom(rngSeed);
            calendar = new ElectionCalendar(eventBus, timeSystem);
            candidateEvaluation = new CandidateEvaluationService(officeSystem.EligibilityService, rng);
            voteSimulator = new ElectionVoteSimulator(rng);
            resultsApplier = new ElectionResultService(officeSystem.AssignOffice, eventBus);
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            LogInfo($"Election calendar synchronized with {timeSystem.Name}.");

            calendar.YearStarted += OnYearStarted;
            calendar.DeclarationWindowOpened += OnDeclarationWindowOpened;
            calendar.ElectionDayArrived += OnElectionDayArrived;
            calendar.Initialize();
        }

        public override void Shutdown()
        {
            calendar.YearStarted -= OnYearStarted;
            calendar.DeclarationWindowOpened -= OnDeclarationWindowOpened;
            calendar.ElectionDayArrived -= OnElectionDayArrived;
            calendar.Shutdown();

            base.Shutdown();
        }

        private void OnYearStarted(OnNewYearEvent e)
        {
            UpdateCurrentDate();

            declarationsByOffice.Clear();
            declarationByCharacter.Clear();

            if (!declarationsByYear.ContainsKey(currentYear))
                declarationsByYear[currentYear] = new List<CandidateDeclaration>();
            if (!resultsByYear.ContainsKey(currentYear))
                resultsByYear[currentYear] = new List<ElectionResultRecord>();
        }

        private void OnDeclarationWindowOpened(OnNewDayEvent e)
        {
            UpdateCurrentDate();
            OpenElectionSeason();
        }

        private void OnElectionDayArrived(OnNewDayEvent e)
        {
            UpdateCurrentDate();
            ConductElections();
        }

        private void UpdateCurrentDate()
        {
            currentYear = calendar.CurrentYear;
            currentMonth = calendar.CurrentMonth;
            currentDay = calendar.CurrentDay;
        }

        private void OpenElectionSeason()
        {
            var infos = officeSystem.GetElectionInfos(currentYear);
            if (infos.Count == 0)
            {
                if (DebugMode)
                    LogInfo($"No magistracies require elections in {currentYear}.");
                return;
            }

            declarationsByOffice.Clear();
            declarationByCharacter.Clear();

            foreach (var info in infos)
            {
                GetOrCreateOfficeDeclarations(info.Definition.Id)?.Clear();
            }

            var allSummaries = new List<ElectionOfficeSummary>();
            foreach (var info in infos)
            {
                allSummaries.Add(new ElectionOfficeSummary
                {
                    OfficeId = info.Definition.Id,
                    OfficeName = info.Definition.Name,
                    Assembly = info.Definition.Assembly,
                    SeatsAvailable = info.SeatsAvailable
                });
            }

            if (!declarationsByYear.ContainsKey(currentYear))
                declarationsByYear[currentYear] = new List<CandidateDeclaration>();

            var living = characterSystem.GetAllLiving();
            foreach (var character in living)
            {
                if (!candidateEvaluation.TryCreateDeclaration(character, infos, currentYear, out var declaration))
                    continue;

                if (declarationByCharacter.TryGetValue(character.ID, out var existingDeclaration))
                {
                    string targetName = existingDeclaration.Office?.Name ?? existingDeclaration.OfficeId;
                    LogWarn($"{character.FullName} already declared for {targetName} in {currentYear}. Skipping duplicate.");
                    continue;
                }

                var officeDeclarations = GetOrCreateOfficeDeclarations(declaration.OfficeId);
                if (officeDeclarations == null)
                {
                    LogWarn($"Unable to register declaration for office '{declaration.Office?.Name ?? declaration.OfficeId}'.");
                }
                else
                {
                    officeDeclarations.Add(declaration);
                }

                declarationsByYear[currentYear].Add(declaration);
                declarationByCharacter[character.ID] = declaration;

                if (DebugMode)
                {
                    var reason = string.Join(", ", declaration.Factors.Select(kv => $"{kv.Key}:{kv.Value:F1}"));
                    LogInfo($"{character.FullName} declares for {declaration.Office.Name} (score {declaration.DesireScore:F1}) [{reason}]");
                }
            }

            eventBus.Publish(new ElectionSeasonOpenedEvent(currentYear, currentMonth, currentDay, allSummaries));
        }

        private void ConductElections()
        {
            LogInfo($"Election day has arrived: conducting elections for {currentYear}.");

            var infos = officeSystem.GetElectionInfos(currentYear);
            if (infos.Count == 0)
            {
                LogInfo($"Election day: no offices scheduled for election in {currentYear}.");
                return;
            }

            if (!resultsByYear.ContainsKey(currentYear))
                resultsByYear[currentYear] = new List<ElectionResultRecord>();

            var summaries = new List<ElectionResultSummary>();

            foreach (var info in infos)
            {
                var declarations = GetOrCreateOfficeDeclarations(info.Definition.Id);
                if (declarations == null)
                {
                    LogWarn($"No declaration bucket available for office '{info.Definition.Id}'.");
                    declarations = new List<CandidateDeclaration>();
                }

                var candidates = new List<ElectionCandidate>();
                foreach (var declaration in declarations)
                {
                    var character = characterSystem.Get(declaration.CharacterId);
                    if (character == null)
                    {
                        LogWarn($"Declaration for office {info.Definition.Id} referenced missing character #{declaration.CharacterId}.");
                        continue;
                    }
                    if (!character.IsAlive)
                        continue;

                    if (!candidateEvaluation.IsEligible(character, info.Definition, currentYear))
                        continue;

                    var candidate = new ElectionCandidate
                    {
                        Declaration = declaration,
                        Character = character,
                        VoteBreakdown = new Dictionary<string, float>(declaration.Factors)
                    };

                    candidates.Add(candidate);
                }

                if (candidates.Count == 0)
                {
                    LogWarn($"{info.Definition.Name}: election skipped, no eligible candidates in {currentYear}.");
                    continue;
                }

                voteSimulator.ScoreCandidates(info.Definition, candidates);

                var candidateSummary = string.Join(", ", candidates
                    .OrderByDescending(c => c.FinalScore)
                    .Select(c => $"{c.Character.FullName} ({c.FinalScore:F1})"));
                LogInfo($"{info.Definition.Name}: candidates -> {candidateSummary}");

                float totalScore = candidates.Sum(c => Mathf.Max(0.1f, c.FinalScore));
                var winners = voteSimulator.SelectWinners(candidates, info.SeatsAvailable);

                var (summary, record) = resultsApplier.ApplyOfficeResults(info.Definition, currentYear, candidates, winners,
                    totalScore, DebugMode, LogInfo, LogWarn);

                summaries.Add(summary);
                resultsByYear[currentYear].Add(record);
            }

            resultsApplier.ClearCandidateState(declarationsByOffice, declarationByCharacter);
            resultsApplier.PublishElectionResults(currentYear, currentMonth, currentDay, summaries);
        }

        private List<CandidateDeclaration> GetOrCreateOfficeDeclarations(string officeId)
        {
            var key = NormalizeOfficeDictionaryKey(officeId);
            if (key == null)
                return null;

            if (!declarationsByOffice.TryGetValue(key, out var list))
            {
                list = new List<CandidateDeclaration>();
                declarationsByOffice[key] = list;
            }

            return list;
        }

        private static string NormalizeOfficeDictionaryKey(string officeId)
        {
            if (string.IsNullOrWhiteSpace(officeId))
                return null;

            return officeId.Trim().ToLowerInvariant();
        }

        public IReadOnlyList<CandidateDeclaration> GetDeclarationsForYear(int year)
        {
            if (declarationsByYear.TryGetValue(year, out var list))
                return list;
            return Array.Empty<CandidateDeclaration>();
        }

        public IReadOnlyList<ElectionResultRecord> GetResultsForYear(int year)
        {
            if (resultsByYear.TryGetValue(year, out var list))
                return list;
            return Array.Empty<ElectionResultRecord>();
        }

        public override Dictionary<string, object> Save()
        {
            try
            {
                var blob = CreateSaveBlob();
                string json = JsonUtility.ToJson(blob);
                return new Dictionary<string, object> { ["json"] = json };
            }
            catch (Exception ex)
            {
                LogError($"Save failed: {ex.Message}");
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null)
                return;

            try
            {
                if (!data.TryGetValue("json", out var raw) || raw is not string json || string.IsNullOrEmpty(json))
                {
                    LogWarn("No valid save data found for ElectionSystem.");
                    return;
                }

                var blob = JsonUtility.FromJson<SaveBlob>(json);
                if (blob == null)
                {
                    LogWarn("ElectionSystem save blob was empty.");
                    return;
                }

                RestoreFromBlob(blob);
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private SaveBlob CreateSaveBlob()
        {
            var blob = new SaveBlob
            {
                Version = 1,
                CurrentYear = currentYear,
                CurrentMonth = currentMonth,
                CurrentDay = currentDay,
                Seed = rngSeed
            };

            foreach (var kvp in declarationsByYear.OrderBy(k => k.Key))
            {
                var declarations = SerializeDeclarations(kvp.Value);
                blob.DeclarationYears.Add(new DeclarationYearBlob
                {
                    Year = kvp.Key,
                    Entries = declarations
                });
            }

            foreach (var kvp in resultsByYear.OrderBy(k => k.Key))
            {
                var records = SerializeResults(kvp.Value);
                blob.ResultYears.Add(new ResultYearBlob
                {
                    Year = kvp.Key,
                    Records = records
                });
            }

            if (declarationsByOffice.Count > 0)
            {
                foreach (var kvp in declarationsByOffice.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var entries = SerializeDeclarations(kvp.Value);
                    if (entries.Count == 0)
                        continue;

                    blob.ActiveDeclarations.Add(new ActiveDeclarationBlob
                    {
                        OfficeId = kvp.Key,
                        Year = currentYear,
                        Entries = entries
                    });
                }
            }

            return blob;
        }

        private void RestoreFromBlob(SaveBlob blob)
        {
            currentYear = blob.CurrentYear;
            currentMonth = blob.CurrentMonth;
            currentDay = blob.CurrentDay;

            rngSeed = blob.Seed != 0 ? blob.Seed : DefaultRngSeed;
            rng.Reset(rngSeed);

            declarationsByYear.Clear();
            declarationsByOffice.Clear();
            declarationByCharacter.Clear();
            resultsByYear.Clear();

            var declarationCache = new Dictionary<(int year, int characterId, string officeId), CandidateDeclaration>();

            if (blob.DeclarationYears != null)
            {
                foreach (var yearEntry in blob.DeclarationYears)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (yearEntry.Entries != null)
                    {
                        foreach (var declarationRecord in yearEntry.Entries)
                        {
                            var declaration = DeserializeDeclaration(declarationRecord, yearEntry.Year, declarationCache);
                            if (declaration != null)
                                list.Add(declaration);
                        }
                    }

                    declarationsByYear[yearEntry.Year] = list;
                }
            }

            if (blob.ResultYears != null)
            {
                foreach (var yearEntry in blob.ResultYears)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<ElectionResultRecord>();
                    if (yearEntry.Records != null)
                    {
                        foreach (var resultRecord in yearEntry.Records)
                        {
                            var record = DeserializeResult(yearEntry.Year, resultRecord, declarationCache);
                            if (record != null)
                                list.Add(record);
                        }
                    }

                    resultsByYear[yearEntry.Year] = list;
                }
            }

            if (blob.ActiveDeclarations != null && blob.ActiveDeclarations.Count > 0)
            {
                foreach (var officeEntry in blob.ActiveDeclarations)
                {
                    if (officeEntry == null)
                        continue;

                    var cacheOfficeKey = string.IsNullOrWhiteSpace(officeEntry.OfficeId)
                        ? null
                        : officeEntry.OfficeId.Trim();
                    var dictionaryKey = NormalizeOfficeDictionaryKey(officeEntry.OfficeId);
                    if (dictionaryKey == null)
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (officeEntry.Entries != null)
                    {
                        int year = officeEntry.Year != 0 ? officeEntry.Year : currentYear;
                        foreach (var declarationRecord in officeEntry.Entries)
                        {
                            CandidateDeclaration declaration = null;
                            if (declarationRecord != null && declarationRecord.CharacterId != 0)
                            {
                                if (cacheOfficeKey != null &&
                                    declarationCache.TryGetValue((year, declarationRecord.CharacterId, cacheOfficeKey), out var cached))
                                    declaration = cached;
                            }

                            declaration ??= DeserializeDeclaration(declarationRecord, year, declarationCache);
                            if (declaration == null)
                                continue;

                            list.Add(declaration);
                            if (declaration.CharacterId != 0)
                                declarationByCharacter[declaration.CharacterId] = declaration;
                        }
                    }

                    declarationsByOffice[dictionaryKey] = list;
                }
            }
            else if (declarationsByYear.TryGetValue(currentYear, out var currentYearDeclarations))
            {
                foreach (var declaration in currentYearDeclarations)
                {
                    if (declaration == null)
                        continue;

                    var dictionaryKey = NormalizeOfficeDictionaryKey(declaration.OfficeId);
                    if (dictionaryKey == null)
                        continue;

                    if (!declarationsByOffice.TryGetValue(dictionaryKey, out var list))
                    {
                        list = new List<CandidateDeclaration>();
                        declarationsByOffice[dictionaryKey] = list;
                    }

                    list.Add(declaration);
                    if (declaration.CharacterId != 0)
                        declarationByCharacter[declaration.CharacterId] = declaration;
                }
            }

            if (!declarationsByYear.ContainsKey(currentYear))
                declarationsByYear[currentYear] = new List<CandidateDeclaration>();

            if (!resultsByYear.ContainsKey(currentYear))
                resultsByYear[currentYear] = new List<ElectionResultRecord>();

            LogInfo($"Loaded election data through {currentYear}.");
        }

        private static List<DeclarationEntry> SerializeDeclarations(IEnumerable<CandidateDeclaration> source)
        {
            var list = new List<DeclarationEntry>();
            if (source == null)
                return list;

            foreach (var declaration in source
                .OrderBy(d => d?.CharacterId ?? 0)
                .ThenBy(d => d?.OfficeId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(d => d?.CharacterName ?? string.Empty, StringComparer.Ordinal))
            {
                var record = SerializeDeclaration(declaration);
                if (record != null)
                    list.Add(record);
            }

            return list;
        }

        private static List<ResultRecordBlob> SerializeResults(IEnumerable<ElectionResultRecord> source)
        {
            var list = new List<ResultRecordBlob>();
            if (source == null)
                return list;

            foreach (var record in source)
            {
                var serialized = SerializeResult(record);
                if (serialized != null)
                    list.Add(serialized);
            }

            return list;
        }

        private static DeclarationEntry SerializeDeclaration(CandidateDeclaration declaration)
        {
            if (declaration == null)
                return null;

            var record = new DeclarationEntry
            {
                CharacterId = declaration.CharacterId,
                CharacterName = declaration.CharacterName,
                OfficeId = declaration.OfficeId,
                DesireScore = declaration.DesireScore,
                Factors = SerializeFactors(declaration.Factors)
            };

            return record;
        }

        private static ResultRecordBlob SerializeResult(ElectionResultRecord record)
        {
            if (record == null || record.Office == null)
                return null;

            var serialized = new ResultRecordBlob
            {
                OfficeId = record.Office.Id
            };

            if (record.Candidates != null)
            {
                foreach (var candidate in record.Candidates)
                {
                    if (candidate == null)
                        continue;

                    var candidateRecord = new CandidateResultBlob
                    {
                        CharacterId = candidate.Character?.ID ?? candidate.Declaration?.CharacterId ?? 0,
                        CharacterName = candidate.Character?.FullName ?? candidate.Declaration?.CharacterName,
                        FinalScore = candidate.FinalScore,
                        Declaration = SerializeDeclaration(candidate.Declaration),
                        Breakdown = SerializeFactors(candidate.VoteBreakdown)
                    };

                    serialized.Candidates.Add(candidateRecord);
                }
            }

            if (record.Winners != null)
            {
                foreach (var winner in record.Winners)
                {
                    if (winner == null)
                        continue;

                    serialized.Winners.Add(new ResultWinnerBlob
                    {
                        CharacterId = winner.CharacterId,
                        CharacterName = winner.CharacterName,
                        SeatIndex = winner.SeatIndex,
                        VoteScore = winner.VoteScore,
                        SupportShare = winner.SupportShare,
                        Notes = winner.Notes
                    });
                }
            }

            return serialized;
        }

        private CandidateDeclaration DeserializeDeclaration(DeclarationEntry record, int year,
            Dictionary<(int year, int characterId, string officeId), CandidateDeclaration> cache)
        {
            if (record == null)
                return null;

            var office = officeSystem.Definitions.GetDefinition(record.OfficeId);

            var declaration = new CandidateDeclaration
            {
                CharacterId = record.CharacterId,
                CharacterName = !string.IsNullOrWhiteSpace(record.CharacterName)
                    ? record.CharacterName
                    : characterSystem.Get(record.CharacterId)?.FullName,
                OfficeId = office?.Id ?? record.OfficeId,
                Office = office,
                DesireScore = record.DesireScore,
                Factors = DeserializeFactors(record.Factors)
            };

            if (cache != null)
            {
                var cacheOfficeKey = string.IsNullOrWhiteSpace(record.OfficeId) ? null : record.OfficeId.Trim();
                if (cacheOfficeKey != null)
                    cache[(year, record.CharacterId, cacheOfficeKey)] = declaration;
            }

            return declaration;
        }

        private ElectionResultRecord DeserializeResult(int year, ResultRecordBlob record,
            Dictionary<(int year, int characterId, string officeId), CandidateDeclaration> cache)
        {
            if (record == null)
                return null;

            var office = officeSystem.Definitions.GetDefinition(record.OfficeId);
            if (office == null)
                return null;

            var result = new ElectionResultRecord
            {
                Office = office,
                Year = year,
                Candidates = new List<ElectionCandidate>(),
                Winners = new List<ElectionWinnerSummary>()
            };

            if (record.Candidates != null)
            {
                foreach (var candidateRecord in record.Candidates)
                {
                    if (candidateRecord == null)
                        continue;

                    int characterId = candidateRecord.CharacterId;
                    var character = characterSystem.Get(characterId);

                    CandidateDeclaration declaration = null;
                    var cacheOfficeKey = string.IsNullOrWhiteSpace(record.OfficeId) ? null : record.OfficeId.Trim();
                    if (cache != null && cacheOfficeKey != null && cache.TryGetValue((year, characterId, cacheOfficeKey), out var cached))
                    {
                        declaration = cached;
                    }
                    else if (candidateRecord.Declaration != null)
                    {
                        declaration = DeserializeDeclaration(candidateRecord.Declaration, year, cache);
                    }

                    declaration ??= new CandidateDeclaration
                    {
                        CharacterId = characterId,
                        CharacterName = !string.IsNullOrWhiteSpace(candidateRecord.CharacterName)
                            ? candidateRecord.CharacterName
                            : character?.FullName,
                        OfficeId = office.Id,
                        Office = office,
                        DesireScore = candidateRecord.Declaration?.DesireScore ?? 0f,
                        Factors = candidateRecord.Declaration != null
                            ? DeserializeFactors(candidateRecord.Declaration.Factors)
                            : new Dictionary<string, float>(StringComparer.Ordinal)
                    };

                    var candidate = new ElectionCandidate
                    {
                        Character = character,
                        Declaration = declaration,
                        FinalScore = candidateRecord.FinalScore,
                        VoteBreakdown = DeserializeFactors(candidateRecord.Breakdown)
                    };

                    result.Candidates.Add(candidate);
                }
            }

            if (record.Winners != null)
            {
                foreach (var winnerRecord in record.Winners)
                {
                    if (winnerRecord == null)
                        continue;

                    result.Winners.Add(new ElectionWinnerSummary
                    {
                        CharacterId = winnerRecord.CharacterId,
                        CharacterName = !string.IsNullOrWhiteSpace(winnerRecord.CharacterName)
                            ? winnerRecord.CharacterName
                            : characterSystem.Get(winnerRecord.CharacterId)?.FullName,
                        SeatIndex = winnerRecord.SeatIndex,
                        VoteScore = winnerRecord.VoteScore,
                        SupportShare = winnerRecord.SupportShare,
                        Notes = winnerRecord.Notes
                    });
                }
            }

            return result;
        }

        private static List<FactorEntry> SerializeFactors(Dictionary<string, float> source)
        {
            var list = new List<FactorEntry>();
            if (source == null || source.Count == 0)
                return list;

            foreach (var kv in source.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                list.Add(new FactorEntry { Key = kv.Key, Value = kv.Value });
            }

            return list;
        }

        private static Dictionary<string, float> DeserializeFactors(List<FactorEntry> entries)
        {
            var dict = new Dictionary<string, float>(StringComparer.Ordinal);
            if (entries == null)
                return dict;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                dict[entry.Key] = entry.Value;
            }

            return dict;
        }

        [Serializable]
        private class SaveBlob
        {
            public int Version;
            public int CurrentYear;
            public int CurrentMonth;
            public int CurrentDay;
            public int Seed;
            public List<DeclarationYearBlob> DeclarationYears = new();
            public List<ResultYearBlob> ResultYears = new();
            public List<ActiveDeclarationBlob> ActiveDeclarations = new();
        }

        [Serializable]
        private class DeclarationYearBlob
        {
            public int Year;
            public List<DeclarationEntry> Entries = new();
        }

        [Serializable]
        private class ResultYearBlob
        {
            public int Year;
            public List<ResultRecordBlob> Records = new();
        }

        [Serializable]
        private class ActiveDeclarationBlob
        {
            public string OfficeId;
            public int Year;
            public List<DeclarationEntry> Entries = new();
        }

        [Serializable]
        private class DeclarationEntry
        {
            public int CharacterId;
            public string CharacterName;
            public string OfficeId;
            public float DesireScore;
            public List<FactorEntry> Factors = new();
        }

        [Serializable]
        private class ResultRecordBlob
        {
            public string OfficeId;
            public List<CandidateResultBlob> Candidates = new();
            public List<ResultWinnerBlob> Winners = new();
        }

        [Serializable]
        private class CandidateResultBlob
        {
            public int CharacterId;
            public string CharacterName;
            public float FinalScore;
            public DeclarationEntry Declaration;
            public List<FactorEntry> Breakdown = new();
        }

        [Serializable]
        private class ResultWinnerBlob
        {
            public int CharacterId;
            public string CharacterName;
            public int SeatIndex;
            public float VoteScore;
            public float SupportShare;
            public string Notes;
        }

        [Serializable]
        private class FactorEntry
        {
            public string Key;
            public float Value;
        }

        private sealed class TrackingRandom : System.Random
        {
            private System.Random inner;

            public TrackingRandom(int seed)
            {
                Reset(seed);
            }

            public void Reset(int seed)
            {
                inner = new System.Random(seed);
            }

            public override double NextDouble()
            {
                return inner.NextDouble();
            }

            public override int Next()
            {
                return inner.Next();
            }

            public override int Next(int maxValue)
            {
                return inner.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                return inner.Next(minValue, maxValue);
            }

            public override void NextBytes(byte[] buffer)
            {
                inner.NextBytes(buffer);
            }
        }
    }
}
