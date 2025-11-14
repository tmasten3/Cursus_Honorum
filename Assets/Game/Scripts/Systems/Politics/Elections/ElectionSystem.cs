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
        private readonly CandidateEvaluation candidateEvaluation;
        private readonly AssemblyVoteSimulator voteSimulator;
        private readonly ElectionResultsApplier resultsApplier;

        private readonly Dictionary<string, List<CandidateDeclaration>> declarationsByOffice = new();
        private readonly Dictionary<int, List<CandidateDeclaration>> declarationsByYear = new();
        private readonly Dictionary<int, List<ElectionResultRecord>> resultsByYear = new();
        private readonly Dictionary<int, CandidateDeclaration> declarationByCharacter = new();

        private readonly System.Random rng = new(15821);

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
            candidateEvaluation = new CandidateEvaluation(officeSystem, rng);
            voteSimulator = new AssemblyVoteSimulator(rng);
            resultsApplier = new ElectionResultsApplier(officeSystem, eventBus);
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

        public override void Update(GameState state) { }

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
    }
}
