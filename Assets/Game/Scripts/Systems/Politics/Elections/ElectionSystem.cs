using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Politics.Offices;
using UnityEngine;

namespace Game.Systems.Politics.Elections
{
    public class ElectionSystem : GameSystemBase
    {
        public override string Name => "Election System";
        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(EventBus.EventBus),
            typeof(TimeSystem.TimeSystem),
            typeof(Game.Systems.CharacterSystem.CharacterSystem),
            typeof(OfficeSystem)
        };

        private readonly EventBus.EventBus eventBus;
        private readonly TimeSystem.TimeSystem timeSystem;
        private readonly Game.Systems.CharacterSystem.CharacterSystem characterSystem;
        private readonly OfficeSystem officeSystem;

        private readonly Dictionary<string, List<CandidateDeclaration>> declarationsByOffice = new();
        private readonly Dictionary<int, List<CandidateDeclaration>> declarationsByYear = new();
        private readonly Dictionary<int, List<ElectionResultRecord>> resultsByYear = new();
        private readonly Dictionary<int, CandidateDeclaration> declarationByCharacter = new();

        private readonly System.Random rng = new(15821);

        private int currentYear;
        private int currentMonth;
        private int currentDay;

        public bool DebugMode { get; set; }
        private bool subscriptionsActive;

        public ElectionSystem(EventBus.EventBus eventBus, TimeSystem.TimeSystem timeSystem,
            Game.Systems.CharacterSystem.CharacterSystem characterSystem, OfficeSystem officeSystem)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            this.officeSystem = officeSystem ?? throw new ArgumentNullException(nameof(officeSystem));
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            LogInfo($"Election calendar synchronized with {timeSystem.Name}.");
            if (!subscriptionsActive)
            {
                eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
                eventBus.Subscribe<OnNewYearEvent>(OnNewYear);
                subscriptionsActive = true;
            }
        }

        public override void Update(GameState state) { }

        public override void Shutdown()
        {
            if (subscriptionsActive)
            {
                eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
                eventBus.Unsubscribe<OnNewYearEvent>(OnNewYear);
                subscriptionsActive = false;
            }

            base.Shutdown();
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            currentYear = e.Year;
            currentMonth = e.Month;
            currentDay = e.Day;

            declarationsByOffice.Clear();
            declarationByCharacter.Clear();
            if (!declarationsByYear.ContainsKey(currentYear))
                declarationsByYear[currentYear] = new List<CandidateDeclaration>();
            if (!resultsByYear.ContainsKey(currentYear))
                resultsByYear[currentYear] = new List<ElectionResultRecord>();
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            currentYear = e.Year;
            currentMonth = e.Month;
            currentDay = e.Day;

            if (e.Month == 6 && e.Day == 1)
                OpenElectionSeason();
            else if (e.Month == 7 && e.Day == 1)
                ConductElections();
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
            var allSummaries = new List<ElectionOfficeSummary>();
            foreach (var info in infos)
            {
                GetOrCreateOfficeDeclarations(info.Definition.Id)?.Clear();
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
                var options = new List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown)>();
                foreach (var info in infos)
                {
                    if (!officeSystem.IsEligible(character, info.Definition, currentYear, out _))
                        continue;

                    var (score, breakdown) = EvaluateAmbition(character, info.Definition, info.SeatsAvailable);
                    if (score <= 0f)
                        continue;

                    options.Add((info.Definition, score, breakdown));
                }

                if (options.Count == 0)
                    continue;

                var prioritized = PrioritizeOfficeChoices(options);
                var choice = WeightedPick(prioritized);
                if (choice.def == null)
                    continue;

                if (declarationByCharacter.TryGetValue(character.ID, out var existingDeclaration))
                {
                    string targetName = existingDeclaration.Office?.Name ?? existingDeclaration.OfficeId;
                    LogWarn($"{character.FullName} already declared for {targetName} in {currentYear}. Skipping duplicate.");
                    continue;
                }

                var declaration = new CandidateDeclaration
                {
                    CharacterId = character.ID,
                    CharacterName = character.FullName,
                    OfficeId = choice.def.Id,
                    Office = choice.def,
                    DesireScore = choice.score,
                    Factors = choice.breakdown
                };

                var officeDeclarations = GetOrCreateOfficeDeclarations(choice.def.Id);
                if (officeDeclarations == null)
                {
                    LogWarn($"Unable to register declaration for office '{choice.def?.Name ?? choice.def?.Id}'.");
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
                    LogInfo($"{character.FullName} declares for {choice.def.Name} (score {choice.score:F1}) [{reason}]");
                }
            }

            eventBus.Publish(new ElectionSeasonOpenedEvent(currentYear, currentMonth, currentDay, allSummaries));
        }

        private (OfficeDefinition def, float score, Dictionary<string, float> breakdown) WeightedPick(
            List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown)> options)
        {
            float total = options.Sum(o => Mathf.Max(0.01f, o.score));
            double roll = rng.NextDouble() * total;
            double accum = 0;
            foreach (var option in options)
            {
                accum += Mathf.Max(0.01f, option.score);
                if (roll <= accum)
                    return option;
            }
            return options.Last();
        }

        private List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown)> PrioritizeOfficeChoices(
            List<(OfficeDefinition def, float score, Dictionary<string, float> breakdown)> options)
        {
            if (options == null || options.Count == 0)
                return options;

            float maxScore = options.Max(o => o.score);
            float threshold = Mathf.Clamp(maxScore * 0.65f, 0f, maxScore);

            var viable = options.Where(o => o.score >= threshold).ToList();
            if (viable.Count == 0)
                viable = options;

            int highestRank = viable.Max(o => o.def?.Rank ?? int.MinValue);
            var prioritized = viable.Where(o => o.def != null && o.def.Rank == highestRank).ToList();

            return prioritized.Count > 0 ? prioritized : viable;
        }

        private (float score, Dictionary<string, float> breakdown) EvaluateAmbition(Character character, OfficeDefinition office, int seats)
        {
            var breakdown = new Dictionary<string, float>();

            float influence = character.Influence * 2.2f;
            breakdown["Influence"] = influence;

            float wealth = Mathf.Sqrt(Mathf.Max(0, character.Wealth)) * 0.6f;
            breakdown["Wealth"] = wealth;

            float ambition = HasTrait(character, "Ambitious") ? 12f : 0f;
            if (HasTrait(character, "Traditional") && office.Assembly == OfficeAssembly.ComitiaCenturiata)
                ambition += 4f;
            if (HasTrait(character, "Populist") && office.Assembly != OfficeAssembly.ComitiaCenturiata)
                ambition += 5f;
            breakdown["Traits"] = ambition;

            float classBonus = character.Class switch
            {
                SocialClass.Patrician => office.Assembly == OfficeAssembly.ComitiaCenturiata ? 10f : 6f,
                SocialClass.Plebeian => office.Assembly == OfficeAssembly.ConciliumPlebis ? 10f : 4f,
                SocialClass.Equestrian => 5f,
                _ => 0f
            };
            breakdown["Status"] = classBonus;

            float ageFactor = Mathf.Clamp01((character.Age - office.MinAge + 8f) / 25f);
            float maturity = ageFactor * 12f;
            breakdown["Maturity"] = maturity;

            float seatOpportunity = Mathf.Log10(seats + 1) * 8f;
            breakdown["Opportunity"] = seatOpportunity;

            float rankPressure = 1f + office.Rank * 0.35f;
            breakdown["Rank"] = rankPressure * 10f;

            float random = (float)(rng.NextDouble() * 6f);
            breakdown["Fortuna"] = random;

            float baseScore = influence + wealth + ambition + classBonus + maturity + seatOpportunity;
            float score = (baseScore * (0.6f + ageFactor) + random) * rankPressure;

            return (score, breakdown);
        }

        private bool HasTrait(Character character, string trait)
        {
            if (character?.Traits == null)
                return false;
            return character.Traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
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

                    if (!officeSystem.IsEligible(character, info.Definition, currentYear, out _))
                        continue;

                    var candidate = new ElectionCandidate
                    {
                        Declaration = declaration,
                        Character = character,
                        VoteBreakdown = new Dictionary<string, float>(declaration.Factors)
                    };

                    var (score, breakdown) = EvaluateVote(info.Definition, candidate);
                    candidate.FinalScore = score;
                    foreach (var kv in breakdown)
                        candidate.VoteBreakdown[kv.Key] = kv.Value;

                    candidates.Add(candidate);
                }

                if (candidates.Count == 0)
                {
                    LogWarn($"{info.Definition.Name}: election skipped, no eligible candidates in {currentYear}.");
                    continue;
                }

                var candidateSummary = string.Join(", ", candidates
                    .OrderByDescending(c => c.FinalScore)
                    .Select(c => $"{c.Character.FullName} ({c.FinalScore:F1})"));
                LogInfo($"{info.Definition.Name}: candidates -> {candidateSummary}");

                float totalScore = candidates.Sum(c => Mathf.Max(0.1f, c.FinalScore));
                var winners = SelectWinners(candidates, info.SeatsAvailable);

                var summary = new ElectionResultSummary
                {
                    OfficeId = info.Definition.Id,
                    OfficeName = info.Definition.Name,
                    Assembly = info.Definition.Assembly,
                    Winners = new List<ElectionWinnerSummary>()
                };

                var record = new ElectionResultRecord
                {
                    Office = info.Definition,
                    Year = currentYear,
                    Candidates = candidates,
                    Winners = new List<ElectionWinnerSummary>()
                };

                var winnerEntries = new List<string>();

                foreach (var winner in winners)
                {
                    var seat = officeSystem.AssignOffice(info.Definition.Id, winner.Character.ID, currentYear);
                    if (seat.SeatIndex < 0)
                    {
                        LogWarn($"{info.Definition.Name}: failed to assign seat for {winner.Character.FullName}.");
                        continue;
                    }
                    float share = Mathf.Max(0.1f, winner.FinalScore) / totalScore;
                    string notes = ComposeWinnerNotes(winner);

                    if (seat.StartYear > currentYear)
                    {
                        string startNote = $"Term begins {seat.StartYear}";
                        notes = string.IsNullOrEmpty(notes) ? startNote : $"{notes}; {startNote}";
                    }

                    var winnerSummary = new ElectionWinnerSummary
                    {
                        CharacterId = winner.Character.ID,
                        CharacterName = winner.Character.FullName,
                        SeatIndex = seat.SeatIndex,
                        VoteScore = winner.FinalScore,
                        SupportShare = share,
                        Notes = notes
                    };

                    summary.Winners.Add(winnerSummary);
                    record.Winners.Add(winnerSummary);

                    winnerEntries.Add($"{winner.Character.FullName} (seat {seat.SeatIndex}, term {seat.StartYear}-{seat.EndYear})");

                    if (DebugMode)
                    {
                        string detail = string.Join(", ", winner.VoteBreakdown
                            .OrderByDescending(kv => kv.Value)
                            .Take(4)
                            .Select(kv => $"{kv.Key}:{kv.Value:F1}"));
                        LogInfo($"{winner.Character.FullName} detailed breakdown -> {detail}");
                    }
                }

                var winnerSummaryLine = string.Join("; ", winnerEntries);
                LogInfo($"{info.Definition.Name}: winners -> {winnerSummaryLine}");

                summaries.Add(summary);
                resultsByYear[currentYear].Add(record);
            }

            declarationsByOffice.Clear();
            declarationByCharacter.Clear();

            if (summaries.Count > 0)
            {
                eventBus.Publish(new ElectionSeasonCompletedEvent(currentYear, currentMonth, currentDay, summaries));
            }
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

        private List<ElectionCandidate> SelectWinners(List<ElectionCandidate> candidates, int seatCount)
        {
            var pool = new List<ElectionCandidate>(candidates);
            var winners = new List<ElectionCandidate>();
            seatCount = Mathf.Max(1, seatCount);

            for (int seat = 0; seat < seatCount && pool.Count > 0; seat++)
            {
                float total = pool.Sum(c => Mathf.Max(0.1f, c.FinalScore));
                double roll = rng.NextDouble() * total;
                double accum = 0;

                for (int i = 0; i < pool.Count; i++)
                {
                    var candidate = pool[i];
                    accum += Mathf.Max(0.1f, candidate.FinalScore);
                    if (roll <= accum)
                    {
                        winners.Add(candidate);
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }

            return winners;
        }

        private (float score, Dictionary<string, float> breakdown) EvaluateVote(OfficeDefinition office, ElectionCandidate candidate)
        {
            var breakdown = new Dictionary<string, float>();
            var c = candidate.Character;

            float influence = c.Influence * 10f;
            float wealth = Mathf.Sqrt(Mathf.Max(0, c.Wealth)) * 1.2f;
            float charisma = HasTrait(c, "Charismatic") ? 12f : 0f;
            float popularity = HasTrait(c, "Popular") ? 10f : c.Influence * 2f;
            float faction = HasTrait(c, "Populist") ? 8f : 4f;

            switch (office.Assembly)
            {
                case OfficeAssembly.ComitiaCenturiata:
                    breakdown["Dignitas"] = influence;
                    breakdown["Wealth"] = wealth * 1.3f;
                    breakdown["Family"] = c.Class == SocialClass.Patrician ? 14f : 6f;
                    breakdown["Allies"] = faction;
                    break;
                case OfficeAssembly.ComitiaTributa:
                    breakdown["Popularity"] = popularity;
                    breakdown["Charisma"] = charisma > 0 ? charisma : c.Influence * 1.5f;
                    breakdown["Wealth"] = wealth * 0.8f;
                    breakdown["Networks"] = c.Class == SocialClass.Plebeian ? 8f : 5f;
                    break;
                case OfficeAssembly.ConciliumPlebis:
                    breakdown["Popularity"] = popularity * 1.2f;
                    breakdown["Advocacy"] = HasTrait(c, "Populist") ? 14f : 6f;
                    breakdown["Plebeian"] = c.Class == SocialClass.Plebeian ? 12f : 2f;
                    breakdown["Charisma"] = charisma > 0 ? charisma : c.Influence * 1.2f;
                    break;
            }

            float ambition = candidate.Declaration?.DesireScore ?? 0f;
            breakdown["Campaign"] = ambition * 0.25f;

            float fortuna = (float)(rng.NextDouble() * 12f);
            breakdown["Fortuna"] = fortuna;

            float total = breakdown.Values.Sum();
            return (total, breakdown);
        }

        private string ComposeWinnerNotes(ElectionCandidate winner)
        {
            if (winner?.VoteBreakdown == null || winner.VoteBreakdown.Count == 0)
                return string.Empty;

            var top = winner.VoteBreakdown.OrderByDescending(kv => kv.Value).Take(2).ToList();
            if (top.Count == 0)
                return string.Empty;

            if (top.Count == 1)
                return $"Backed by {top[0].Key.ToLower()}";

            return $"Backed by {top[0].Key.ToLower()} and {top[1].Key.ToLower()}";
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
