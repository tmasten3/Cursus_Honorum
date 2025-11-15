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

        private readonly ElectionCalendar calendar;
        private CandidateEvaluationService candidateEvaluation;
        private ElectionVoteSimulator voteSimulator;
        private readonly ElectionResultService resultsApplier;

        private readonly Dictionary<string, List<CandidateDeclaration>> declarationsByOffice = new();
        private readonly Dictionary<int, List<CandidateDeclaration>> declarationsByYear = new();
        private readonly Dictionary<int, List<ElectionResultRecord>> resultsByYear = new();
        private readonly Dictionary<int, CandidateDeclaration> declarationByCharacter = new();

        private TrackedRandom rng;
        private int rngSeed = DefaultSeed;
        private const int DefaultSeed = 15821;

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

            calendar = new ElectionCalendar(eventBus, timeSystem);
            InitializeRandom(DefaultSeed, 0);
            resultsApplier = new ElectionResultService(officeSystem.AssignOffice, eventBus);
        }

        private void InitializeRandom(int seed, int sampleCount)
        {
            if (seed == 0)
                seed = DefaultSeed;

            rngSeed = seed;
            rng = new TrackedRandom(seed, Math.Max(0, sampleCount));
            candidateEvaluation = new CandidateEvaluationService(officeSystem.EligibilityService, rng);
            voteSimulator = new ElectionVoteSimulator(rng);
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
                var blob = new SaveBlob
                {
                    Seed = rngSeed,
                    SampleCount = rng?.SampleCount ?? 0,
                    CurrentYear = currentYear,
                    CurrentMonth = currentMonth,
                    CurrentDay = currentDay,
                    Declarations = SerializeDeclarations(),
                    Results = SerializeResults()
                };

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

                var blob = JsonUtility.FromJson<SaveBlob>(json) ?? new SaveBlob();

                InitializeRandom(blob.Seed, blob.SampleCount);

                currentYear = blob.CurrentYear;
                currentMonth = blob.CurrentMonth;
                currentDay = blob.CurrentDay;

                declarationsByOffice.Clear();
                declarationByCharacter.Clear();
                declarationsByYear.Clear();
                resultsByYear.Clear();

                if (blob.Declarations != null)
                {
                    foreach (var year in blob.Declarations)
                    {
                        if (year == null)
                            continue;

                        var list = new List<CandidateDeclaration>();
                        if (year.Declarations != null)
                        {
                            foreach (var record in year.Declarations)
                            {
                                if (record == null || string.IsNullOrWhiteSpace(record.OfficeId))
                                    continue;

                                var declaration = new CandidateDeclaration
                                {
                                    CharacterId = record.CharacterId,
                                    CharacterName = record.CharacterName,
                                    OfficeId = record.OfficeId,
                                    Office = officeSystem.GetDefinition(record.OfficeId),
                                    DesireScore = record.DesireScore,
                                    Factors = DeserializeFactors(record.Factors)
                                };

                                list.Add(declaration);
                            }
                        }

                        declarationsByYear[year.Year] = list;
                    }
                }

                if (blob.Results != null)
                {
                    foreach (var year in blob.Results)
                    {
                        if (year == null)
                            continue;

                        var records = new List<ElectionResultRecord>();
                        if (year.Records != null)
                        {
                            foreach (var record in year.Records)
                            {
                                if (record == null)
                                    continue;

                                string officeId = record.OfficeId;
                                var office = !string.IsNullOrWhiteSpace(officeId) ? officeSystem.GetDefinition(officeId) : null;

                                var result = new ElectionResultRecord
                                {
                                    Office = office,
                                    Year = year.Year,
                                    Candidates = new List<ElectionCandidate>(),
                                    Winners = new List<ElectionWinnerSummary>()
                                };

                                if (record.Candidates != null)
                                {
                                    foreach (var candidateData in record.Candidates)
                                    {
                                        if (candidateData == null)
                                            continue;

                                        var candidate = new ElectionCandidate
                                        {
                                            FinalScore = candidateData.FinalScore,
                                            VoteBreakdown = DeserializeFactors(candidateData.VoteBreakdown)
                                        };

                                        int candidateId = candidateData.CharacterId;
                                        if (candidateId != 0)
                                            candidate.Character = characterSystem.Get(candidateId);

                                        CandidateDeclaration declaration = null;
                                        if (candidateData.Declaration != null && !string.IsNullOrWhiteSpace(candidateData.Declaration.OfficeId))
                                        {
                                            declaration = new CandidateDeclaration
                                            {
                                                CharacterId = candidateData.Declaration.CharacterId,
                                                CharacterName = candidateData.Declaration.CharacterName,
                                                OfficeId = candidateData.Declaration.OfficeId,
                                                Office = officeSystem.GetDefinition(candidateData.Declaration.OfficeId),
                                                DesireScore = candidateData.Declaration.DesireScore,
                                                Factors = DeserializeFactors(candidateData.Declaration.Factors)
                                            };
                                        }
                                        else if (!string.IsNullOrWhiteSpace(officeId))
                                        {
                                            declaration = new CandidateDeclaration
                                            {
                                                CharacterId = candidateId,
                                                CharacterName = candidate.Character?.FullName,
                                                OfficeId = officeId,
                                                Office = office,
                                                DesireScore = 0f,
                                                Factors = new Dictionary<string, float>()
                                            };
                                        }

                                        candidate.Declaration = declaration;
                                        result.Candidates.Add(candidate);
                                    }
                                }

                                if (record.Winners != null)
                                {
                                    foreach (var winner in record.Winners)
                                    {
                                        if (winner == null)
                                            continue;

                                        result.Winners.Add(new ElectionWinnerSummary
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

                                if (result.Office == null && !string.IsNullOrWhiteSpace(record.OfficeId))
                                    result.Office = officeSystem.GetDefinition(record.OfficeId);

                                if (result.Office == null)
                                {
                                    var officeFromCandidate = result.Candidates.FirstOrDefault(c => c?.Declaration?.Office != null)?.Declaration.Office;
                                    if (officeFromCandidate != null)
                                        result.Office = officeFromCandidate;
                                }

                                if (!string.IsNullOrWhiteSpace(record.OfficeId) || result.Candidates.Count > 0 || result.Winners.Count > 0)
                                    records.Add(result);
                            }
                        }

                        if (records.Count > 0)
                            resultsByYear[year.Year] = records;
                    }
                }

                RebuildCurrentYearIndexes();
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private void RebuildCurrentYearIndexes()
        {
            declarationsByOffice.Clear();
            declarationByCharacter.Clear();

            if (!declarationsByYear.TryGetValue(currentYear, out var list) || list == null)
                return;

            foreach (var declaration in list)
            {
                if (declaration == null)
                    continue;

                GetOrCreateOfficeDeclarations(declaration.OfficeId)?.Add(declaration);
                declarationByCharacter[declaration.CharacterId] = declaration;
            }
        }

        private List<YearDeclarationsRecord> SerializeDeclarations()
        {
            var result = new List<YearDeclarationsRecord>();
            foreach (var kv in declarationsByYear)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                    continue;

                var yearRecord = new YearDeclarationsRecord { Year = kv.Key };
                foreach (var declaration in kv.Value)
                {
                    if (declaration == null)
                        continue;

                    yearRecord.Declarations.Add(new DeclarationRecord
                    {
                        CharacterId = declaration.CharacterId,
                        CharacterName = declaration.CharacterName,
                        OfficeId = declaration.OfficeId,
                        DesireScore = declaration.DesireScore,
                        Factors = SerializeFactors(declaration.Factors)
                    });
                }

                if (yearRecord.Declarations.Count > 0)
                    result.Add(yearRecord);
            }

            return result;
        }

        private List<YearResultsRecord> SerializeResults()
        {
            var result = new List<YearResultsRecord>();
            foreach (var kv in resultsByYear)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                    continue;

                var yearRecord = new YearResultsRecord { Year = kv.Key };
                foreach (var record in kv.Value)
                {
                    if (record == null)
                        continue;

                    var resultRecord = new ResultRecord
                    {
                        OfficeId = record.Office?.Id,
                        Candidates = new List<CandidateResultRecord>(),
                        Winners = new List<WinnerRecord>()
                    };

                    foreach (var candidate in record.Candidates ?? Enumerable.Empty<ElectionCandidate>())
                    {
                        if (candidate == null)
                            continue;

                        resultRecord.Candidates.Add(new CandidateResultRecord
                        {
                            CharacterId = candidate.Character?.ID ?? candidate.Declaration?.CharacterId ?? 0,
                            FinalScore = candidate.FinalScore,
                            VoteBreakdown = SerializeFactors(candidate.VoteBreakdown),
                            Declaration = candidate.Declaration == null
                                ? null
                                : new DeclarationRecord
                                {
                                    CharacterId = candidate.Declaration.CharacterId,
                                    CharacterName = candidate.Declaration.CharacterName,
                                    OfficeId = candidate.Declaration.OfficeId,
                                    DesireScore = candidate.Declaration.DesireScore,
                                    Factors = SerializeFactors(candidate.Declaration.Factors)
                                }
                        });
                    }

                    foreach (var winner in record.Winners ?? Enumerable.Empty<ElectionWinnerSummary>())
                    {
                        if (winner == null)
                            continue;

                        resultRecord.Winners.Add(new WinnerRecord
                        {
                            CharacterId = winner.CharacterId,
                            CharacterName = winner.CharacterName,
                            SeatIndex = winner.SeatIndex,
                            VoteScore = winner.VoteScore,
                            SupportShare = winner.SupportShare,
                            Notes = winner.Notes
                        });
                    }

                    if (string.IsNullOrWhiteSpace(resultRecord.OfficeId))
                    {
                        var declarationOffice = resultRecord.Candidates
                            .Select(c => c.Declaration?.OfficeId)
                            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
                        if (!string.IsNullOrWhiteSpace(declarationOffice))
                            resultRecord.OfficeId = declarationOffice;
                    }

                    if (resultRecord.Candidates.Count > 0 || resultRecord.Winners.Count > 0 || !string.IsNullOrWhiteSpace(resultRecord.OfficeId))
                        yearRecord.Records.Add(resultRecord);
                }

                if (yearRecord.Records.Count > 0)
                    result.Add(yearRecord);
            }

            return result;
        }

        private static List<FactorRecord> SerializeFactors(Dictionary<string, float> source)
        {
            var result = new List<FactorRecord>();
            if (source == null)
                return result;

            foreach (var kv in source)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                result.Add(new FactorRecord { Key = kv.Key, Value = kv.Value });
            }

            return result;
        }

        private static Dictionary<string, float> DeserializeFactors(List<FactorRecord> factors)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (factors == null)
                return result;

            foreach (var factor in factors)
            {
                if (factor == null || string.IsNullOrWhiteSpace(factor.Key))
                    continue;

                result[factor.Key] = factor.Value;
            }

            return result;
        }

        [Serializable]
        private class SaveBlob
        {
            public int Version = 1;
            public int Seed = DefaultSeed;
            public int SampleCount;
            public int CurrentYear;
            public int CurrentMonth;
            public int CurrentDay;
            public List<YearDeclarationsRecord> Declarations = new();
            public List<YearResultsRecord> Results = new();
        }

        [Serializable]
        private class YearDeclarationsRecord
        {
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
            public List<FactorRecord> Factors = new();
        }

        [Serializable]
        private class YearResultsRecord
        {
            public int Year;
            public List<ResultRecord> Records = new();
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
            public float FinalScore;
            public List<FactorRecord> VoteBreakdown = new();
            public DeclarationRecord Declaration;
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
        private class FactorRecord
        {
            public string Key;
            public float Value;
        }

        private sealed class TrackedRandom : System.Random
        {
            public int SampleCount { get; private set; }

            public TrackedRandom(int seed) : base(seed)
            {
            }

            public TrackedRandom(int seed, int sampleCount) : base(seed)
            {
                Advance(sampleCount);
            }

            public override double NextDouble()
            {
                SampleCount++;
                return base.NextDouble();
            }

            public override int Next()
            {
                SampleCount++;
                return base.Next();
            }

            public override int Next(int maxValue)
            {
                SampleCount++;
                return base.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                SampleCount++;
                return base.Next(minValue, maxValue);
            }

            public override void NextBytes(byte[] buffer)
            {
                SampleCount++;
                base.NextBytes(buffer);
            }

            public void Advance(int count)
            {
                if (count <= 0)
                    return;

                for (int i = 0; i < count; i++)
                {
                    base.Next();
                }

                SampleCount = count;
            }
        }
    }
}
