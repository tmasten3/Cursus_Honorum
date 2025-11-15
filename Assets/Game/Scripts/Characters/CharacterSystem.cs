using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Core;
using Logger = Game.Core.Logger;
using Game.Data.Characters;
using Game.Systems.Characters;
using Game.Systems.EventBus;
using Game.Systems.Time;

namespace Game.Systems.CharacterSystem
{
    public class CharacterSystem : GameSystemBase
    {
        public override string Name => "Character System";
        public override IEnumerable<Type> Dependencies => new[] { typeof(EventBus.EventBus), typeof(TimeSystem) };

        private readonly EventBus.EventBus bus;
        private readonly TimeSystem timeSystem;
        private readonly SimulationConfig.CharacterSettings settings;

        private System.Random rng;
        private int rngSeed;
        private readonly CharacterRepository repository = new CharacterRepository();
        private readonly DailyPopulationMetrics metrics = new DailyPopulationMetrics();
        private readonly CharacterDataLoader dataLoader = new CharacterDataLoader();
        private readonly CharacterAgeService ageService;
        private readonly CharacterFamilyService familyService;
        private readonly CharacterMarriageService marriageService;
        private CharacterMortalityService mortality;
        private int curYear, curMonth, curDay;
        private EventSubscription newDaySubscription = EventSubscription.Empty;
        private EventSubscription newYearSubscription = EventSubscription.Empty;

        public CharacterSystem(EventBus.EventBus bus, TimeSystem timeSystem, SimulationConfig simulationConfig)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            if (simulationConfig == null) throw new ArgumentNullException(nameof(simulationConfig));

            settings = simulationConfig.Character ?? throw new ArgumentNullException(nameof(simulationConfig.Character));
            rngSeed = settings.RngSeed;

            ageService = new CharacterAgeService(repository);
            familyService = new CharacterFamilyService(repository);
            marriageService = new CharacterMarriageService(repository);
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);

            rng = new System.Random(rngSeed);

            if (settings.Mortality?.AgeBands != null)
            {
                foreach (var band in settings.Mortality.AgeBands)
                    band.DailyHazard = YearlyToDaily(band.YearlyHazard);
            }

            var baseCharacters = dataLoader.LoadBaseCharacters(settings.BaseDataPath);
            foreach (var character in baseCharacters)
            {
                if (character == null)
                {
                    Logger.Warn("Safety", $"{settings.BaseDataPath}: Encountered null character entry during load. Skipping.");
                    continue;
                }

                try
                {
                    CharacterFactory.EnsureLifecycleState(character, character.BirthYear + character.Age);
                    familyService.AddCharacter(character, settings.KeepDeadInMemory);
                }
                catch (Exception ex)
                {
                    Logger.Error("Safety", $"{settings.BaseDataPath}: Failed to add character #{character.ID} to repository: {ex.Message}");
                }
            }

            mortality = new CharacterMortalityService(repository, rng, GetDailyHazard);
            newDaySubscription.Dispose();
            newYearSubscription.Dispose();
            newDaySubscription = bus.Subscribe<OnNewDayEvent>(OnNewDay);
            newYearSubscription = bus.Subscribe<OnNewYearEvent>(OnNewYear);

            LogInfo($"Initialized with {repository.AliveCount} living characters across {repository.FamilyCount} families (time source: {timeSystem.Name}).");
        }

        public CharacterValidationResult ValidateBaseCharactersOnly()
        {
            if (settings == null)
                throw new InvalidOperationException("Character settings are not configured.");

            CharacterFactory.LoadBaseCharacters(settings.BaseDataPath, CharacterLoadMode.Strict);
            return CharacterFactory.LastValidationResult;
        }

        public override void Shutdown()
        {
            newDaySubscription.Dispose();
            newYearSubscription.Dispose();
            newDaySubscription = EventSubscription.Empty;
            newYearSubscription = EventSubscription.Empty;

            base.Shutdown();
            repository.Reset();
            metrics.Reset();
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            curYear = e.Year;
            curMonth = e.Month;
            curDay = e.Day;

            metrics.Reset();
            ageService.ProcessDailyAging(curMonth, curDay);

            foreach (var id in mortality.SelectDailyDeaths())
                Kill(id, "Daily hazard");

            bus.Publish(metrics.ToEvent(curYear, curMonth, curDay));
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            curYear = e.Year; curMonth = e.Month; curDay = e.Day;
            ProcessAnnualLifecycle(e.Year, e.Month, e.Day);
            LogInfo($"Year {e.Year} begins. Alive: {repository.AliveCount}, Families: {repository.FamilyCount}");
        }

        public Character Get(int id) => familyService.Get(id);
        public IReadOnlyList<Character> GetAllLiving() => familyService.GetAllLiving();
        public IReadOnlyList<Character> GetByFamily(string gens) => familyService.GetByFamily(gens);
        public IReadOnlyList<Character> GetByCognomen(string cognomen) => familyService.GetByCognomen(cognomen);
        public IReadOnlyList<Character> GetByBranch(string branchId) => familyService.GetByBranch(branchId);
        public IReadOnlyList<Character> GetByClass(SocialClass c) => familyService.GetByClass(c);
        public int CountAlive() => familyService.CountAlive();
        public int GetLiveCharacterCount() => familyService.CountAlive();
        public int GetFamilyCount() => familyService.GetFamilyCount();
        internal bool TryGetRepository(out CharacterRepository repo) { repo = repository; return repo != null; }

        public void AddCharacter(Character character)
        {
            int? evaluationYear = null;
            if (curYear != 0)
                evaluationYear = curYear;
            else if (character != null)
                evaluationYear = character.BirthYear + character.Age;

            CharacterFactory.EnsureLifecycleState(character, evaluationYear);
            familyService.AddCharacter(character, settings.KeepDeadInMemory); metrics.RecordBirth();
        }

        public bool Kill(int id, string cause = "Natural causes")
        {
            var character = repository.MarkDead(id, settings.KeepDeadInMemory);
            if (character == null) return false;

            metrics.RecordDeath();
            bus.Publish(new OnCharacterDied(curYear, curMonth, curDay, id, cause));
            return true;
        }

        public bool Marry(int a, int b)
        {
            if (!marriageService.TryMarry(a, b)) return false;

            metrics.RecordMarriage();
            return true;
        }

        [Serializable]
        private class SaveBlob
        {
            public int Version;
            public int Seed;
            public List<Character> Characters = new List<Character>();
            public List<int> AliveIDs = new List<int>();
            public List<int> DeadIDs = new List<int>();
        }

        public override Dictionary<string, object> Save()
        {
            try
            {
                var blob = new SaveBlob
                {
                    Version = 1,
                    Seed = rngSeed,
                    Characters = repository.AllCharacters.Select(c => c).ToList(),
                    AliveIDs = repository.AliveIdSnapshot().ToList(),
                    DeadIDs = settings.KeepDeadInMemory ? repository.DeadIdSnapshot().ToList() : new List<int>()
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
            if (data == null) return;

            try
            {
                if (!data.TryGetValue("json", out var raw) || raw is not string json || string.IsNullOrEmpty(json))
                {
                    LogWarn("No valid save data found for CharacterSystem.");
                    return;
                }

                var blob = JsonUtility.FromJson<SaveBlob>(json);
                repository.Reset();
                foreach (var character in blob.Characters)
                {
                    CharacterFactory.NormalizeDeserializedCharacter(character, "SaveLoad");
                    CharacterFactory.EnsureLifecycleState(character, character.BirthYear + character.Age);
                    repository.Add(character, settings.KeepDeadInMemory);
                }
                repository.ApplyLifeState(blob.AliveIDs, blob.DeadIDs, settings.KeepDeadInMemory);
                rngSeed = blob.Seed;
                settings.RngSeed = rngSeed;
                rng = new System.Random(rngSeed);
                mortality = new CharacterMortalityService(repository, rng, GetDailyHazard);
                metrics.Reset();

                LogInfo($"Loaded {repository.AliveCount} living characters.");
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private float GetDailyHazard(int age)
        {
            if (settings.Mortality == null || !settings.Mortality.UseAgeBandHazards)
                return 0f;

            foreach (var band in settings.Mortality.AgeBands)
            {
                if (age >= band.Min && age <= band.Max)
                    return band.DailyHazard;
            }

            return 0f;
        }

        private static float YearlyToDaily(float yearly)
        {
            yearly = Mathf.Clamp01(yearly);
            return 1f - Mathf.Pow(1f - yearly, 1f / 365f);
        }

        private void ProcessAnnualLifecycle(int year, int month, int day)
        {
            var living = familyService.GetAllLiving();
            foreach (var character in living)
            {
                CharacterFactory.EnsureLifecycleState(character, year);
                UpdateAmbitionProfile(character, year, month, day);
                ProcessTraitDevelopment(character, year, month, day);
                ApplyRetirementIfNeeded(character, year, month, day);
            }
        }

        private void UpdateAmbitionProfile(Character character, int year, int month, int day)
        {
            var ambition = character.Ambition ?? AmbitionProfile.CreateDefault(character);
            character.Ambition = ambition;

            var previousGoal = ambition.CurrentGoal ?? AmbitionProfile.InferDefaultGoal(character);
            var desiredGoal = DetermineCurrentAmbitionGoal(character);
            bool goalChanged = !string.Equals(previousGoal, desiredGoal, StringComparison.Ordinal);

            int previousIntensity = ambition.Intensity;
            int desiredIntensity = GetDesiredAmbitionIntensity(character);
            int delta = Mathf.Clamp(desiredIntensity - ambition.Intensity, -3, 3);
            bool intensityChanged = delta != 0;

            int? previousTarget = ambition.TargetYear;

            if (goalChanged)
            {
                ambition.CurrentGoal = desiredGoal;
                EnsureAmbitionHistoryList(ambition);
                ambition.History.Add(new AmbitionHistoryRecord
                {
                    Year = year,
                    Description = $"Shifted focus to {desiredGoal}",
                    Outcome = null
                });
            }

            if (intensityChanged)
            {
                ambition.Intensity = Mathf.Clamp(ambition.Intensity + delta, 0, 100);
                ambition.LastEvaluatedYear = year;
                MaybeRecordAmbitionHistory(ambition, year, previousIntensity, ambition.Intensity);
            }
            else if (ambition.LastEvaluatedYear < year)
            {
                ambition.LastEvaluatedYear = year;
            }

            if (!ambition.IsRetired)
            {
                if (ambition.Intensity >= 50 && (!ambition.TargetYear.HasValue || ambition.TargetYear < year))
                {
                    ambition.TargetYear = year + 5;
                    EnsureAmbitionHistoryList(ambition);
                    ambition.History.Add(new AmbitionHistoryRecord
                    {
                        Year = year,
                        Description = $"Set target year to {ambition.TargetYear}",
                        Outcome = null
                    });
                }
                else if (ambition.Intensity <= 10 && ambition.TargetYear.HasValue && ambition.TargetYear > year + 1)
                {
                    ambition.TargetYear = year + 1;
                }
            }

            bool targetChanged = previousTarget != ambition.TargetYear;

            if (goalChanged || intensityChanged || targetChanged)
            {
                bus.Publish(new OnCharacterAmbitionChanged(year, month, day, character.ID, previousGoal, ambition.CurrentGoal,
                    previousIntensity, ambition.Intensity, previousTarget, ambition.TargetYear, ambition.IsRetired));
            }
        }

        private void ProcessTraitDevelopment(Character character, int year, int month, int day)
        {
            if (character.TraitRecords == null || character.TraitRecords.Count == 0)
                return;

            foreach (var record in character.TraitRecords.OrderBy(r => r?.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id))
                    continue;

                float gain = GetAnnualTraitExperienceGain(character, record);
                if (gain <= 0f)
                    continue;

                int previousLevel = record.Level;
                record.Experience += gain;

                float threshold = GetTraitLevelThreshold(record.Level);
                if (record.Experience >= threshold)
                {
                    record.Experience -= threshold;
                    record.Level += 1;

                    bus.Publish(new OnCharacterTraitAdvanced(year, month, day, character.ID, record.Id, previousLevel, record.Level));

                    RecordCareerMilestone(character, year, month, day, $"Trait mastery: {record.Id}",
                        $"Advanced to level {record.Level}");
                }
            }
        }

        private void ApplyRetirementIfNeeded(Character character, int year, int month, int day)
        {
            var ambition = character.Ambition;
            if (ambition == null || ambition.IsRetired)
                return;

            bool shouldRetire = character.Age >= 65 || (character.Age >= 55 && ambition.Intensity <= 15);
            if (!shouldRetire)
                return;

            var previousGoal = ambition.CurrentGoal;
            var previousTarget = ambition.TargetYear;
            var previousIntensity = ambition.Intensity;

            ambition.IsRetired = true;
            ambition.CurrentGoal = "Retired";
            ambition.TargetYear = null;
            ambition.Intensity = 0;
            ambition.LastEvaluatedYear = year;
            EnsureAmbitionHistoryList(ambition);
            ambition.History.Add(new AmbitionHistoryRecord
            {
                Year = year,
                Description = "Retired from public life",
                Outcome = "Retired"
            });

            var notes = $"Retired at age {character.Age}";
            bus.Publish(new OnCharacterRetired(year, month, day, character.ID, previousGoal, notes));

            RecordCareerMilestone(character, year, month, day, "Retired from public life", notes);

            bus.Publish(new OnCharacterAmbitionChanged(year, month, day, character.ID, previousGoal, ambition.CurrentGoal,
                previousIntensity, ambition.Intensity, previousTarget, ambition.TargetYear, true));
        }

        private int GetDesiredAmbitionIntensity(Character character)
        {
            int baseValue = character.Class switch
            {
                SocialClass.Patrician => 65,
                SocialClass.Equestrian => 55,
                _ => 45
            };

            if (character.Age < 25)
                baseValue += 10;
            else if (character.Age < 35)
                baseValue += 5;
            else if (character.Age < 45)
                baseValue += 0;
            else if (character.Age < 55)
                baseValue -= 10;
            else if (character.Age < 65)
                baseValue -= 20;
            else
                baseValue = 0;

            return Mathf.Clamp(baseValue, 0, 90);
        }

        private string DetermineCurrentAmbitionGoal(Character character)
        {
            if (character.Ambition != null && character.Ambition.IsRetired)
                return "Retired";

            return character.Class switch
            {
                SocialClass.Patrician when character.Age < 25 => "Secure patronage",
                SocialClass.Patrician when character.Age < 35 => "Quaestorship",
                SocialClass.Patrician when character.Age < 45 => "Praetorship",
                SocialClass.Patrician when character.Age < 55 => "Consulship",
                SocialClass.Patrician => "Mentor clients",
                SocialClass.Equestrian when character.Age < 30 => "Cavalry command",
                SocialClass.Equestrian when character.Age < 45 => "Praetorian duties",
                SocialClass.Equestrian => "Advise magistrates",
                _ when character.Age < 30 => "Tribunate",
                _ when character.Age < 45 => "Aedileship",
                _ => "Support faction"
            };
        }

        private void MaybeRecordAmbitionHistory(AmbitionProfile ambition, int year, int previousIntensity, int newIntensity)
        {
            if (ambition == null)
                return;

            EnsureAmbitionHistoryList(ambition);
            foreach (var threshold in ambitionMilestoneThresholds)
            {
                if (previousIntensity < threshold && newIntensity >= threshold)
                {
                    ambition.History.Add(new AmbitionHistoryRecord
                    {
                        Year = year,
                        Description = $"Ambition intensity reached {threshold}",
                        Outcome = null
                    });
                }
            }
        }

        private float GetAnnualTraitExperienceGain(Character character, TraitRecord record)
        {
            float baseGain = 2f;
            if (character.Ambition != null && !character.Ambition.IsRetired)
                baseGain += character.Ambition.Intensity / 25f;

            if (record.Level <= 1)
                baseGain += 0.5f;

            if (character.Age < 30)
                baseGain += 0.5f;

            return baseGain;
        }

        private float GetTraitLevelThreshold(int level)
        {
            return 10f + (level * 5f);
        }

        private void RecordCareerMilestone(Character character, int year, int month, int day, string title, string notes)
        {
            EnsureCareerMilestoneList(character);

            bool alreadyRecorded = character.CareerMilestones.Any(m =>
                m != null && string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase) && m.Year == year);

            if (alreadyRecorded)
                return;

            var milestone = new CareerMilestone
            {
                Title = title,
                Year = year,
                Notes = notes
            };

            character.CareerMilestones.Add(milestone);

            bus.Publish(new OnCharacterCareerMilestoneRecorded(year, month, day, character.ID, title, notes));
        }

        private static void EnsureAmbitionHistoryList(AmbitionProfile ambition)
        {
            if (ambition == null)
                return;

            if (ambition.History == null)
                ambition.History = new List<AmbitionHistoryRecord>();
        }

        private static void EnsureCareerMilestoneList(Character character)
        {
            if (character == null)
                return;

            if (character.CareerMilestones == null)
                character.CareerMilestones = new List<CareerMilestone>();
        }

        private static readonly int[] ambitionMilestoneThresholds = { 25, 50, 75 };
    }
}
