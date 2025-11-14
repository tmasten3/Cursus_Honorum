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
            if (string.IsNullOrWhiteSpace(officeId))
                return null;

            var key = officeId.Trim().ToLowerInvariant();
            if (!declarationsByOffice.TryGetValue(key, out var list))
            {
                list = new List<CandidateDeclaration>();
                declarationsByOffice[key] = list;
            }

            return list;
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
                Seed = rngSeed,
                SampleCount = rng.SampleCount
            };

            foreach (var kvp in declarationsByYear.OrderBy(k => k.Key))
            {
                var entry = new YearlyDeclarations
                {
                    Year = kvp.Key,
                    Declarations = SerializeDeclarations(kvp.Value)
                };
                blob.Declarations.Add(entry);
            }

            foreach (var kvp in resultsByYear.OrderBy(k => k.Key))
            {
                var entry = new YearlyResults
                {
                    Year = kvp.Key,
                    Results = SerializeResults(kvp.Value)
                };
                blob.Results.Add(entry);
            }

            if (declarationsByOffice.Count > 0)
            {
                foreach (var kvp in declarationsByOffice.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var entry = new ActiveOfficeDeclarations
                    {
                        OfficeId = kvp.Key,
                        Year = currentYear,
                        Declarations = SerializeDeclarations(kvp.Value)
                    };

                    if (entry.Declarations.Count > 0)
                        blob.ActiveDeclarations.Add(entry);
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
            rng.Reset(rngSeed, Math.Max(0, blob.SampleCount));

            declarationsByYear.Clear();
            declarationsByOffice.Clear();
            declarationByCharacter.Clear();
            resultsByYear.Clear();

            var declarationCache = new Dictionary<(int year, int characterId, string officeId), CandidateDeclaration>();

            if (blob.Declarations != null)
            {
                foreach (var yearEntry in blob.Declarations)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (yearEntry.Declarations != null)
                    {
                        foreach (var declarationRecord in yearEntry.Declarations)
                        {
                            var declaration = DeserializeDeclaration(declarationRecord, yearEntry.Year, declarationCache);
                            if (declaration != null)
                                list.Add(declaration);
                        }
                    }

                    declarationsByYear[yearEntry.Year] = list;
                }
            }

            if (blob.Results != null)
            {
                foreach (var yearEntry in blob.Results)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<ElectionResultRecord>();
                    if (yearEntry.Results != null)
                    {
                        foreach (var resultRecord in yearEntry.Results)
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

                    string normalized = OfficeDefinitions.NormalizeOfficeId(officeEntry.OfficeId);
                    if (normalized == null)
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (officeEntry.Declarations != null)
                    {
                        int year = officeEntry.Year != 0 ? officeEntry.Year : currentYear;
                        foreach (var declarationRecord in officeEntry.Declarations)
                        {
                            CandidateDeclaration declaration = null;
                            if (declarationRecord != null && declarationRecord.CharacterId != 0)
                            {
                                if (declarationCache.TryGetValue((year, declarationRecord.CharacterId, normalized), out var cached))
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

                    declarationsByOffice[normalized] = list;
                }
            }
            else if (declarationsByYear.TryGetValue(currentYear, out var currentYearDeclarations))
            {
                foreach (var declaration in currentYearDeclarations)
                {
                    if (declaration == null)
                        continue;

                    string normalized = OfficeDefinitions.NormalizeOfficeId(declaration.OfficeId);
                    if (normalized == null)
                        continue;

                    if (!declarationsByOffice.TryGetValue(normalized, out var list))
                    {
                        list = new List<CandidateDeclaration>();
                        declarationsByOffice[normalized] = list;
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

        private static List<DeclarationRecord> SerializeDeclarations(IEnumerable<CandidateDeclaration> source)
        {
            var list = new List<DeclarationRecord>();
            if (source == null)
                return list;

            foreach (var declaration in source)
            {
                var record = SerializeDeclaration(declaration);
                if (record != null)
                    list.Add(record);
            }

            return list;
        }

        private static List<ResultRecord> SerializeResults(IEnumerable<ElectionResultRecord> source)
        {
            var list = new List<ResultRecord>();
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

        private static DeclarationRecord SerializeDeclaration(CandidateDeclaration declaration)
        {
            if (declaration == null)
                return null;

            var record = new DeclarationRecord
            {
                CharacterId = declaration.CharacterId,
                CharacterName = declaration.CharacterName,
                OfficeId = declaration.OfficeId,
                DesireScore = declaration.DesireScore,
                Factors = SerializeFactors(declaration.Factors)
            };

            return record;
        }

        private static ResultRecord SerializeResult(ElectionResultRecord record)
        {
            if (record == null || record.Office == null)
                return null;

            var serialized = new ResultRecord
            {
                OfficeId = record.Office.Id
            };

            if (record.Candidates != null)
            {
                foreach (var candidate in record.Candidates)
                {
                    if (candidate == null)
                        continue;

                    var candidateRecord = new CandidateResultRecord
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

                    serialized.Winners.Add(new WinnerRecord
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

        private CandidateDeclaration DeserializeDeclaration(DeclarationRecord record, int year,
            Dictionary<(int year, int characterId, string officeId), CandidateDeclaration> cache)
        {
            if (record == null)
                return null;

            string normalizedOfficeId = OfficeDefinitions.NormalizeOfficeId(record.OfficeId) ?? record.OfficeId;
            var office = officeSystem.Definitions.GetDefinition(normalizedOfficeId);

            var declaration = new CandidateDeclaration
            {
                CharacterId = record.CharacterId,
                CharacterName = !string.IsNullOrWhiteSpace(record.CharacterName)
                    ? record.CharacterName
                    : characterSystem.Get(record.CharacterId)?.FullName,
                OfficeId = office?.Id ?? normalizedOfficeId,
                Office = office,
                DesireScore = record.DesireScore,
                Factors = DeserializeFactors(record.Factors)
            };

            if (cache != null && normalizedOfficeId != null)
                cache[(year, record.CharacterId, normalizedOfficeId)] = declaration;

            return declaration;
        }

        private ElectionResultRecord DeserializeResult(int year, ResultRecord record,
            Dictionary<(int year, int characterId, string officeId), CandidateDeclaration> cache)
        {
            if (record == null)
                return null;

            string normalizedOfficeId = OfficeDefinitions.NormalizeOfficeId(record.OfficeId) ?? record.OfficeId;
            var office = officeSystem.Definitions.GetDefinition(normalizedOfficeId);
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
                    if (cache != null && normalizedOfficeId != null && cache.TryGetValue((year, characterId, normalizedOfficeId), out var cached))
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
            public int SampleCount;
            public List<YearlyDeclarations> Declarations = new();
            public List<YearlyResults> Results = new();
            public List<ActiveOfficeDeclarations> ActiveDeclarations = new();
        }

        [Serializable]
        private class YearlyDeclarations
        {
            public int Year;
            public List<DeclarationRecord> Declarations = new();
        }

        [Serializable]
        private class YearlyResults
        {
            public int Year;
            public List<ResultRecord> Results = new();
        }

        [Serializable]
        private class ActiveOfficeDeclarations
        {
            public string OfficeId;
            public int Year;
            public List<DeclarationRecord> Declarations = new();
        }

        [Serializable]
        private class DeclarationRecord
        {
            public int CharacterId;
            public string CharacterName;
            public string OfficeId;
            public float DesireScore;
            public List<FactorEntry> Factors = new();
        }

        [Serializable]
        private class ResultRecord
        {
            public string OfficeId;
            public List<CandidateResultRecord> Candidates = new();
            public List<WinnerRecord> Winners = new();
        }

        [Serializable]
        private class CandidateResultRecord
        {
            public int CharacterId;
            public string CharacterName;
            public float FinalScore;
            public DeclarationRecord Declaration;
            public List<FactorEntry> Breakdown = new();
        }

        [Serializable]
        private class WinnerRecord
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
            private int sampleCount;

            public TrackingRandom(int seed)
            {
                Reset(seed, 0);
            }

            public int SampleCount => sampleCount;

            public void Reset(int seed, int consumedSamples)
            {
                inner = new System.Random(seed);
                sampleCount = 0;
                if (consumedSamples > 0)
                {
                    for (int i = 0; i < consumedSamples; i++)
                        _ = inner.Next();
                    sampleCount = consumedSamples;
                }
            }

            public override double NextDouble()
            {
                sampleCount++;
                return inner.NextDouble();
            }

            public override int Next()
            {
                sampleCount++;
                return inner.Next();
            }

            public override int Next(int maxValue)
            {
                sampleCount++;
                return inner.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                sampleCount++;
                return inner.Next(minValue, maxValue);
            }

            public override void NextBytes(byte[] buffer)
            {
                inner.NextBytes(buffer);
                if (buffer != null && buffer.Length > 0)
                    sampleCount++;
            }
        }
    }
}
