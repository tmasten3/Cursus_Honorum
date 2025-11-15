using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        private const int SaveVersion = 1;
        private const int DefaultRngSeed = 15821;

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

            calendar = new ElectionCalendar(eventBus, timeSystem);
            resultsApplier = new ElectionResultService(officeSystem.AssignOffice, eventBus);

            RestoreRandomState(DefaultRngSeed, 0);
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

        public override Dictionary<string, object> Save()
        {
            var data = new Dictionary<string, object>
            {
                ["version"] = SaveVersion,
                ["rngSeed"] = rngSeed,
                ["rngSamples"] = rng?.SampleCount ?? 0,
                ["currentYear"] = currentYear,
                ["currentMonth"] = currentMonth,
                ["currentDay"] = currentDay
            };

            var years = new List<object>();
            foreach (var year in EnumerateTrackedYears())
            {
                years.Add(new Dictionary<string, object>
                {
                    ["year"] = year,
                    ["declarations"] = SerializeDeclarations(year),
                    ["results"] = SerializeResults(year)
                });
            }

            data["years"] = years;
            return data;
        }

        public override void Load(Dictionary<string, object> data)
        {
            declarationsByYear.Clear();
            resultsByYear.Clear();
            declarationsByOffice.Clear();
            declarationByCharacter.Clear();

            if (data == null)
            {
                RestoreRandomState(DefaultRngSeed, 0);
                return;
            }

            rngSeed = ReadInt(data, "rngSeed", DefaultRngSeed);
            int sampleCount = ReadInt(data, "rngSamples", 0);
            currentYear = ReadInt(data, "currentYear", currentYear);
            currentMonth = ReadInt(data, "currentMonth", currentMonth);
            currentDay = ReadInt(data, "currentDay", currentDay);

            if (data.TryGetValue("years", out var yearsObj))
            {
                foreach (var entry in EnumerateList(yearsObj))
                {
                    if (entry is not Dictionary<string, object> yearData)
                        continue;

                    int year = ReadInt(yearData, "year", int.MinValue);
                    if (year == int.MinValue)
                        continue;

                    var declarations = DeserializeDeclarations(yearData);
                    var results = DeserializeResults(yearData, year);

                    declarationsByYear[year] = declarations;
                    resultsByYear[year] = results;
                }
            }

            RestoreRandomState(rngSeed, sampleCount);
            RebuildActiveYearCaches();
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

        private IEnumerable<int> EnumerateTrackedYears()
        {
            var keys = new HashSet<int>();
            foreach (var year in declarationsByYear.Keys)
                keys.Add(year);
            foreach (var year in resultsByYear.Keys)
                keys.Add(year);

            return keys.OrderBy(y => y);
        }

        private List<object> SerializeDeclarations(int year)
        {
            var serialized = new List<object>();
            if (!declarationsByYear.TryGetValue(year, out var declarations) || declarations == null)
                return serialized;

            foreach (var declaration in declarations.OrderBy(d => d?.CharacterId ?? 0))
            {
                if (declaration == null)
                    continue;

                var factors = new Dictionary<string, object>();
                if (declaration.Factors != null)
                {
                    foreach (var kv in declaration.Factors)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key))
                            continue;
                        factors[kv.Key] = kv.Value;
                    }
                }

                serialized.Add(new Dictionary<string, object>
                {
                    ["characterId"] = declaration.CharacterId,
                    ["characterName"] = declaration.CharacterName ?? string.Empty,
                    ["officeId"] = declaration.OfficeId ?? string.Empty,
                    ["desireScore"] = declaration.DesireScore,
                    ["factors"] = factors
                });
            }

            return serialized;
        }

        private List<object> SerializeResults(int year)
        {
            var serialized = new List<object>();
            if (!resultsByYear.TryGetValue(year, out var records) || records == null)
                return serialized;

            foreach (var record in records.OrderBy(r => r?.Office?.Id ?? string.Empty))
            {
                if (record == null)
                    continue;

                var winners = new List<object>();
                if (record.Winners != null)
                {
                    foreach (var winner in record.Winners)
                    {
                        if (winner == null)
                            continue;

                        winners.Add(new Dictionary<string, object>
                        {
                            ["characterId"] = winner.CharacterId,
                            ["characterName"] = winner.CharacterName ?? string.Empty,
                            ["seatIndex"] = winner.SeatIndex,
                            ["voteScore"] = winner.VoteScore,
                            ["supportShare"] = winner.SupportShare,
                            ["notes"] = winner.Notes ?? string.Empty
                        });
                    }
                }

                serialized.Add(new Dictionary<string, object>
                {
                    ["officeId"] = record.Office?.Id ?? string.Empty,
                    ["officeName"] = record.Office?.Name ?? string.Empty,
                    ["assembly"] = (int)(record.Office?.Assembly ?? OfficeAssembly.ComitiaCenturiata),
                    ["winners"] = winners
                });
            }

            return serialized;
        }

        private static IEnumerable<object> EnumerateList(object source)
        {
            if (source is IList list)
            {
                foreach (var item in list)
                    yield return item;
            }
        }

        private static int ReadInt(Dictionary<string, object> source, string key, int defaultValue)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        private static float ReadFloat(Dictionary<string, object> source, string key, float defaultValue)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return ConvertToFloat(value, defaultValue);
        }

        private static float ConvertToFloat(object value, float defaultValue)
        {
            if (value == null)
                return defaultValue;

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                if (value is string s && float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
                return string.Empty;

            return value as string ?? value.ToString();
        }

        private Dictionary<string, float> DeserializeFactors(Dictionary<string, object> source)
        {
            var result = new Dictionary<string, float>();
            if (source == null || !source.TryGetValue("factors", out var raw) || raw == null)
                return result;

            if (raw is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is not string key || string.IsNullOrWhiteSpace(key))
                        continue;

                    result[key] = ConvertToFloat(entry.Value, 0f);
                }
            }

            return result;
        }

        private List<CandidateDeclaration> DeserializeDeclarations(Dictionary<string, object> source)
        {
            var list = new List<CandidateDeclaration>();
            if (source == null || !source.TryGetValue("declarations", out var raw))
                return list;

            foreach (var entry in EnumerateList(raw))
            {
                if (entry is not Dictionary<string, object> declData)
                    continue;

                int characterId = ReadInt(declData, "characterId", 0);
                if (characterId == 0)
                    continue;

                string officeId = ReadString(declData, "officeId");
                var declaration = new CandidateDeclaration
                {
                    CharacterId = characterId,
                    CharacterName = ReadString(declData, "characterName"),
                    OfficeId = officeId,
                    Office = officeSystem.GetDefinition(officeId),
                    DesireScore = ReadFloat(declData, "desireScore", 0f),
                    Factors = DeserializeFactors(declData)
                };

                list.Add(declaration);
            }

            return list;
        }

        private List<ElectionResultRecord> DeserializeResults(Dictionary<string, object> source, int year)
        {
            var list = new List<ElectionResultRecord>();
            if (source == null || !source.TryGetValue("results", out var raw))
                return list;

            foreach (var entry in EnumerateList(raw))
            {
                if (entry is not Dictionary<string, object> recordData)
                    continue;

                string officeId = ReadString(recordData, "officeId");
                string officeName = ReadString(recordData, "officeName");
                int assemblyValue = ReadInt(recordData, "assembly", (int)OfficeAssembly.ComitiaCenturiata);

                var definition = officeSystem.GetDefinition(officeId);
                if (definition == null && !string.IsNullOrWhiteSpace(officeId))
                {
                    definition = new OfficeDefinition
                    {
                        Id = OfficeDefinitions.NormalizeOfficeId(officeId) ?? officeId,
                        Name = string.IsNullOrEmpty(officeName) ? officeId : officeName,
                        Assembly = Enum.IsDefined(typeof(OfficeAssembly), assemblyValue)
                            ? (OfficeAssembly)assemblyValue
                            : OfficeAssembly.ComitiaCenturiata
                    };
                }

                var record = new ElectionResultRecord
                {
                    Office = definition,
                    Year = year,
                    Candidates = new List<ElectionCandidate>(),
                    Winners = new List<ElectionWinnerSummary>()
                };

                if (recordData.TryGetValue("winners", out var winnersRaw))
                {
                    foreach (var winnerEntry in EnumerateList(winnersRaw))
                    {
                        if (winnerEntry is not Dictionary<string, object> winnerData)
                            continue;

                        var winner = new ElectionWinnerSummary
                        {
                            CharacterId = ReadInt(winnerData, "characterId", 0),
                            CharacterName = ReadString(winnerData, "characterName"),
                            SeatIndex = ReadInt(winnerData, "seatIndex", 0),
                            VoteScore = ReadFloat(winnerData, "voteScore", 0f),
                            SupportShare = ReadFloat(winnerData, "supportShare", 0f),
                            Notes = ReadString(winnerData, "notes")
                        };

                        record.Winners.Add(winner);
                    }
                }

                list.Add(record);
            }

            return list;
        }

        private void RestoreRandomState(int seed, int sampleCount)
        {
            rngSeed = seed;
            rng = CreateRandom(seed, sampleCount);
            candidateEvaluation = new CandidateEvaluationService(officeSystem.EligibilityService, rng);
            voteSimulator = new ElectionVoteSimulator(rng);
        }

        private static TrackedRandom CreateRandom(int seed, int sampleCount)
        {
            var random = new TrackedRandom(seed);
            if (sampleCount > 0)
            {
                for (int i = 0; i < sampleCount; i++)
                    random.NextDouble();
            }

            return random;
        }

        private void RebuildActiveYearCaches()
        {
            if (!declarationsByYear.TryGetValue(currentYear, out var declarations) || declarations == null)
                return;

            foreach (var declaration in declarations)
            {
                if (declaration == null)
                    continue;

                var officeDeclarations = GetOrCreateOfficeDeclarations(declaration.OfficeId);
                officeDeclarations?.Add(declaration);

                if (!declarationByCharacter.ContainsKey(declaration.CharacterId))
                    declarationByCharacter[declaration.CharacterId] = declaration;
            }
        }

        private sealed class TrackedRandom : System.Random
        {
            public int SampleCount { get; private set; }

            public TrackedRandom(int seed) : base(seed)
            {
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
        }
    }
}
