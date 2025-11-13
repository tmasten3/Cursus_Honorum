using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Core;
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
        private readonly CharacterRepository repository = new();
        private readonly DailyPopulationMetrics metrics = new();
        private readonly CharacterDataLoader dataLoader = new();
        private readonly CharacterAgeService ageService;
        private readonly CharacterFamilyService familyService;
        private readonly CharacterMarriageService marriageService;
        private CharacterMortalityService mortality;
        private int curYear, curMonth, curDay;
        private bool subscriptionsActive;

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
                    familyService.AddCharacter(character, settings.KeepDeadInMemory);
                }
                catch (Exception ex)
                {
                    Logger.Error("Safety", $"{settings.BaseDataPath}: Failed to add character #{character.ID} to repository: {ex.Message}");
                }
            }

            mortality = new CharacterMortalityService(repository, rng, GetDailyHazard);
            if (!subscriptionsActive)
            {
                bus.Subscribe<OnNewDayEvent>(OnNewDay);
                bus.Subscribe<OnNewYearEvent>(OnNewYear);
                subscriptionsActive = true;
            }

            LogInfo($"Initialized with {repository.AliveCount} living characters across {repository.FamilyCount} families (time source: {timeSystem.Name}).");
        }

        public override void Update(GameState state) { }

        public override void Shutdown()
        {
            if (subscriptionsActive)
            {
                bus.Unsubscribe<OnNewDayEvent>(OnNewDay);
                bus.Unsubscribe<OnNewYearEvent>(OnNewYear);
                subscriptionsActive = false;
            }

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
            LogInfo($"Year {e.Year} begins. Alive: {repository.AliveCount}, Families: {repository.FamilyCount}");
        }

        public Character Get(int id) => familyService.Get(id);
        public IReadOnlyList<Character> GetAllLiving() => familyService.GetAllLiving();
        public IReadOnlyList<Character> GetByFamily(string gens) => familyService.GetByFamily(gens);
        public IReadOnlyList<Character> GetByClass(SocialClass c) => familyService.GetByClass(c);
        public int CountAlive() => familyService.CountAlive();
        public int GetLiveCharacterCount() => familyService.CountAlive();
        public int GetFamilyCount() => familyService.GetFamilyCount();
        internal bool TryGetRepository(out CharacterRepository repo) { repo = repository; return repo != null; }

        public void AddCharacter(Character character)
        {
            familyService.AddCharacter(character, settings.KeepDeadInMemory);
            metrics.RecordBirth();
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
            public List<Character> Characters = new();
            public List<int> AliveIDs = new();
            public List<int> DeadIDs = new();
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
                    repository.Add(character, settings.KeepDeadInMemory);

                repository.ApplyLifeState(blob.AliveIDs, blob.DeadIDs, settings.KeepDeadInMemory);

                rngSeed = blob.Seed;
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
    }
}
