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

        private readonly SeededRandom rng;
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

            rng = new SeededRandom(rngSeed);
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
                version = 1,
                currentYear = currentYear,
                currentMonth = currentMonth,
                currentDay = currentDay,
                seed = rngSeed
            };

            foreach (var kvp in declarationsByYear.OrderBy(k => k.Key))
            {
                var entry = new DeclarationYearBlob
                {
                    year = kvp.Key,
                    declarations = SerializeDeclarations(kvp.Value)
                };
                blob.declarationYears.Add(entry);
            }

            foreach (var kvp in resultsByYear.OrderBy(k => k.Key))
            {
                var entry = new ResultYearBlob
                {
                    year = kvp.Key,
                    records = SerializeResults(kvp.Value)
                };
                blob.resultYears.Add(entry);
            }

            if (declarationsByOffice.Count > 0)
            {
                var active = new ActiveDeclarationsBlob
                {
                    year = currentYear
                };

                foreach (var kvp in declarationsByOffice.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var officeBlob = new OfficeDeclarationBlob
                    {
                        officeId = kvp.Key,
                        declarations = SerializeDeclarations(kvp.Value)
                    };

                    if (officeBlob.declarations.Count > 0)
                        active.offices.Add(officeBlob);
                }

                if (active.offices.Count > 0)
                    blob.activeDeclarations = active;
            }

            return blob;
        }

        private void RestoreFromBlob(SaveBlob blob)
        {
            currentYear = blob.currentYear;
            currentMonth = blob.currentMonth;
            currentDay = blob.currentDay;

            rngSeed = blob.seed != 0 ? blob.seed : DefaultRngSeed;
            rng.Reset(rngSeed);

            declarationsByYear.Clear();
            declarationsByOffice.Clear();
            declarationByCharacter.Clear();
            resultsByYear.Clear();

            var declarationCache = new Dictionary<(int year, int characterId, string officeId), CandidateDeclaration>();

            if (blob.declarationYears != null)
            {
                foreach (var yearEntry in blob.declarationYears)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (yearEntry.declarations != null)
                    {
                        foreach (var declarationRecord in yearEntry.declarations)
                        {
                            var declaration = DeserializeDeclaration(declarationRecord, yearEntry.year, declarationCache);
                            if (declaration != null)
                                list.Add(declaration);
                        }
                    }

                    declarationsByYear[yearEntry.year] = list;
                }
            }

            if (blob.resultYears != null)
            {
                foreach (var yearEntry in blob.resultYears)
                {
                    if (yearEntry == null)
                        continue;

                    var list = new List<ElectionResultRecord>();
                    if (yearEntry.records != null)
                    {
                        foreach (var resultRecord in yearEntry.records)
                        {
                            var record = DeserializeResult(yearEntry.year, resultRecord, declarationCache);
                            if (record != null)
                                list.Add(record);
                        }
                    }

                    resultsByYear[yearEntry.year] = list;
                }
            }

            if (blob.activeDeclarations != null && blob.activeDeclarations.offices != null && blob.activeDeclarations.offices.Count > 0)
            {
                int activeYear = blob.activeDeclarations.year != 0 ? blob.activeDeclarations.year : currentYear;

                foreach (var officeEntry in blob.activeDeclarations.offices)
                {
                    if (officeEntry == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(officeEntry.officeId))
                        continue;

                    var list = new List<CandidateDeclaration>();
                    if (officeEntry.declarations != null)
                    {
                        foreach (var declarationRecord in officeEntry.declarations)
                        {
                            CandidateDeclaration declaration = null;
                            if (declarationRecord != null && declarationRecord.characterId != 0)
                            {
                                if (declarationCache.TryGetValue((activeYear, declarationRecord.characterId, officeEntry.officeId), out var cached))
                                    declaration = cached;
                            }

                            declaration ??= DeserializeDeclaration(declarationRecord, activeYear, declarationCache);
                            if (declaration == null)
                                continue;

                            list.Add(declaration);
                            if (declaration.CharacterId != 0)
                                declarationByCharacter[declaration.CharacterId] = declaration;
                        }
                    }

                    declarationsByOffice[officeEntry.officeId] = list;
                }
            }
            else if (declarationsByYear.TryGetValue(currentYear, out var currentYearDeclarations))
            {
                foreach (var declaration in currentYearDeclarations)
                {
                    if (declaration == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(declaration.OfficeId))
                        continue;

                    if (!declarationsByOffice.TryGetValue(declaration.OfficeId, out var list))
                    {
                        list = new List<CandidateDeclaration>();
                        declarationsByOffice[declaration.OfficeId] = list;
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

            var ordered = source.Where(d => d != null)
                .OrderBy(d => d.OfficeId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(d => d.CharacterId)
                .ThenBy(d => d.CharacterName ?? string.Empty, StringComparer.Ordinal);

            foreach (var declaration in ordered)
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
                characterId = declaration.CharacterId,
                characterName = declaration.CharacterName,
                officeId = declaration.OfficeId,
                desireScore = declaration.DesireScore,
                factors = SerializeFactors(declaration.Factors)
            };

            return record;
        }

        private static ResultRecordBlob SerializeResult(ElectionResultRecord record)
        {
            if (record == null || record.Office == null)
                return null;

            var serialized = new ResultRecordBlob
            {
                officeId = record.Office.Id
            };

            if (record.Candidates != null)
            {
                foreach (var candidate in record.Candidates)
                {
                    if (candidate == null)
                        continue;

                    var candidateRecord = new CandidateResultBlob
                    {
                        characterId = candidate.Character?.ID ?? candidate.Declaration?.CharacterId ?? 0,
                        characterName = candidate.Character?.FullName ?? candidate.Declaration?.CharacterName,
                        finalScore = candidate.FinalScore,
                        declaration = SerializeDeclaration(candidate.Declaration),
                        breakdown = SerializeFactors(candidate.VoteBreakdown)
                    };

                    serialized.candidates.Add(candidateRecord);
                }
            }

            if (record.Winners != null)
            {
                foreach (var winner in record.Winners)
                {
                    if (winner == null)
                        continue;

                    serialized.winners.Add(new WinnerRecordBlob
                    {
                        characterId = winner.CharacterId,
                        characterName = winner.CharacterName,
                        seatIndex = winner.SeatIndex,
                        voteScore = winner.VoteScore,
                        supportShare = winner.SupportShare,
                        notes = winner.Notes
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

            string officeId = record.officeId;
            var office = !string.IsNullOrWhiteSpace(officeId)
                ? officeSystem.Definitions.GetDefinition(officeId)
                : null;

            var declaration = new CandidateDeclaration
            {
                CharacterId = record.characterId,
                CharacterName = !string.IsNullOrWhiteSpace(record.characterName)
                    ? record.characterName
                    : characterSystem.Get(record.characterId)?.FullName,
                OfficeId = office?.Id ?? officeId,
                Office = office,
                DesireScore = record.desireScore,
                Factors = DeserializeFactors(record.factors)
            };

            if (cache != null && !string.IsNullOrWhiteSpace(declaration.OfficeId))
                cache[(year, record.characterId, declaration.OfficeId)] = declaration;

            return declaration;
        }

        private ElectionResultRecord DeserializeResult(int year, ResultRecordBlob record,
            Dictionary<(int year, int characterId, string officeId), CandidateDeclaration> cache)
        {
            if (record == null)
                return null;

            string officeId = record.officeId;
            var office = !string.IsNullOrWhiteSpace(officeId)
                ? officeSystem.Definitions.GetDefinition(officeId)
                : null;
            if (office == null)
                return null;

            var result = new ElectionResultRecord
            {
                Office = office,
                Year = year,
                Candidates = new List<ElectionCandidate>(),
                Winners = new List<ElectionWinnerSummary>()
            };

            if (record.candidates != null)
            {
                foreach (var candidateRecord in record.candidates)
                {
                    if (candidateRecord == null)
                        continue;

                    int characterId = candidateRecord.characterId;
                    var character = characterSystem.Get(characterId);

                    CandidateDeclaration declaration = null;
                    if (cache != null && !string.IsNullOrWhiteSpace(office.Id) && cache.TryGetValue((year, characterId, office.Id), out var cached))
                    {
                        declaration = cached;
                    }
                    else if (candidateRecord.declaration != null)
                    {
                        declaration = DeserializeDeclaration(candidateRecord.declaration, year, cache);
                    }

                    declaration ??= new CandidateDeclaration
                    {
                        CharacterId = characterId,
                        CharacterName = !string.IsNullOrWhiteSpace(candidateRecord.characterName)
                            ? candidateRecord.characterName
                            : character?.FullName,
                        OfficeId = office.Id,
                        Office = office,
                        DesireScore = candidateRecord.declaration?.desireScore ?? 0f,
                        Factors = candidateRecord.declaration != null
                            ? DeserializeFactors(candidateRecord.declaration.factors)
                            : new Dictionary<string, float>(StringComparer.Ordinal)
                    };

                    var candidate = new ElectionCandidate
                    {
                        Character = character,
                        Declaration = declaration,
                        FinalScore = candidateRecord.finalScore,
                        VoteBreakdown = DeserializeFactors(candidateRecord.breakdown)
                    };

                    result.Candidates.Add(candidate);
                }
            }

            if (record.winners != null)
            {
                foreach (var winnerRecord in record.winners)
                {
                    if (winnerRecord == null)
                        continue;

                    result.Winners.Add(new ElectionWinnerSummary
                    {
                        CharacterId = winnerRecord.characterId,
                        CharacterName = !string.IsNullOrWhiteSpace(winnerRecord.characterName)
                            ? winnerRecord.characterName
                            : characterSystem.Get(winnerRecord.characterId)?.FullName,
                        SeatIndex = winnerRecord.seatIndex,
                        VoteScore = winnerRecord.voteScore,
                        SupportShare = winnerRecord.supportShare,
                        Notes = winnerRecord.notes
                    });
                }
            }

            return result;
        }

        private static List<FactorRecord> SerializeFactors(Dictionary<string, float> source)
        {
            var list = new List<FactorRecord>();
            if (source == null || source.Count == 0)
                return list;

            foreach (var kv in source.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                list.Add(new FactorRecord { key = kv.Key, value = kv.Value });
            }

            return list;
        }

        private static Dictionary<string, float> DeserializeFactors(List<FactorRecord> entries)
        {
            var dict = new Dictionary<string, float>(StringComparer.Ordinal);
            if (entries == null)
                return dict;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                dict[entry.key] = entry.value;
            }

            return dict;
        }

        [Serializable]
        private class SaveBlob
        {
            public int version;
            public int currentYear;
            public int currentMonth;
            public int currentDay;
            public int seed;
            public List<DeclarationYearBlob> declarationYears = new();
            public List<ResultYearBlob> resultYears = new();
            public ActiveDeclarationsBlob activeDeclarations;
        }

        [Serializable]
        private class DeclarationYearBlob
        {
            public int year;
            public List<DeclarationEntry> declarations = new();
        }

        [Serializable]
        private class ResultYearBlob
        {
            public int year;
            public List<ResultRecordBlob> records = new();
        }

        [Serializable]
        private class ActiveDeclarationsBlob
        {
            public int year;
            public List<OfficeDeclarationBlob> offices = new();
        }

        [Serializable]
        private class OfficeDeclarationBlob
        {
            public string officeId;
            public List<DeclarationEntry> declarations = new();
        }

        [Serializable]
        private class DeclarationEntry
        {
            public int characterId;
            public string characterName;
            public string officeId;
            public float desireScore;
            public List<FactorRecord> factors = new();
        }

        [Serializable]
        private class ResultRecordBlob
        {
            public string officeId;
            public List<CandidateResultBlob> candidates = new();
            public List<WinnerRecordBlob> winners = new();
        }

        [Serializable]
        private class CandidateResultBlob
        {
            public int characterId;
            public string characterName;
            public float finalScore;
            public DeclarationEntry declaration;
            public List<FactorRecord> breakdown = new();
        }

        [Serializable]
        private class WinnerRecordBlob
        {
            public int characterId;
            public string characterName;
            public int seatIndex;
            public float voteScore;
            public float supportShare;
            public string notes;
        }

        [Serializable]
        private class FactorRecord
        {
            public string key;
            public float value;
        }

        private sealed class SeededRandom : System.Random
        {
            private System.Random inner;

            public SeededRandom(int seed)
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
