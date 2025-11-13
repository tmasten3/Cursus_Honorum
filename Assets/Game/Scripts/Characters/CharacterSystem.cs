using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.TimeSystem;

namespace Game.Systems.CharacterSystem
{
    /// <summary>
    /// Core registry for all characters in the simulation.
    /// Responsible for data storage, lifecycle processing, and event emission.
    /// </summary>
    public class CharacterSystem : GameSystemBase
    {
        public override string Name => "Character System";
        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(EventBus.EventBus), typeof(TimeSystem.TimeSystem) };

        [Serializable]
        private class MortalityBand
        {
            public int Min;
            public int Max;
            public float YearlyHazard;
            [NonSerialized] public float DailyHazard;
        }

        [Serializable]
        private class Config
        {
            public int Version = 1;
            public int RngSeed = 1337;
            public bool KeepDeadInMemory = true;
            public string BaseDataPath = "Assets/Game/Data/base_characters.json";

            [Serializable]
            public class MortalityCfg
            {
                public bool UseAgeBandHazards = true;
                public MortalityBand[] AgeBands = new MortalityBand[]
                {
                    new MortalityBand{ Min=0,  Max=4,   YearlyHazard=0.08f },
                    new MortalityBand{ Min=5,  Max=14,  YearlyHazard=0.01f },
                    new MortalityBand{ Min=15, Max=29,  YearlyHazard=0.007f },
                    new MortalityBand{ Min=30, Max=44,  YearlyHazard=0.012f },
                    new MortalityBand{ Min=45, Max=59,  YearlyHazard=0.03f },
                    new MortalityBand{ Min=60, Max=74,  YearlyHazard=0.08f },
                    new MortalityBand{ Min=75, Max=110, YearlyHazard=0.20f },
                };
            }
            public MortalityCfg Mortality = new();
        }

        private readonly EventBus.EventBus bus;
        private readonly TimeSystem.TimeSystem timeSystem;

        private Config config = new();
        private System.Random rng;

        private readonly CharacterRepository repository = new();
        private readonly DailyPopulationMetrics metrics = new();
        private CharacterMortalityService mortality;

        private int curYear, curMonth, curDay;
        private bool subscriptionsActive;

        public CharacterSystem(EventBus.EventBus bus, TimeSystem.TimeSystem timeSystem)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
        }

        private List<Character> SafeLoadBaseCharacters(string path)
        {
            try
            {
                var characters = CharacterFactory.LoadBaseCharacters(path) ?? new List<Character>();
                foreach (var character in characters)
                {
                    if (character == null)
                        continue;

                    if (character.RomanName == null)
                        Game.Core.Logger.Warn("Safety", $"{path}: Character #{character.ID} missing RomanName definition.");

                    if (string.IsNullOrWhiteSpace(character.Family))
                        Game.Core.Logger.Warn("Safety", $"{path}: Character #{character.ID} missing family information.");
                }

                return characters;
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Error("Safety", $"Failed to load base character file '{path}': {ex.Message}");
                return new List<Character>();
            }
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);

            rng = new System.Random(config.RngSeed);

            foreach (var band in config.Mortality.AgeBands)
                band.DailyHazard = YearlyToDaily(band.YearlyHazard);

            var baseCharacters = SafeLoadBaseCharacters(config.BaseDataPath);
            foreach (var character in baseCharacters)
            {
                if (character == null)
                {
                    Game.Core.Logger.Warn("Safety", $"{config.BaseDataPath}: Encountered null character entry during load. Skipping.");
                    continue;
                }

                try
                {
                    repository.Add(character, config.KeepDeadInMemory);
                }
                catch (Exception ex)
                {
                    Game.Core.Logger.Error("Safety", $"{config.BaseDataPath}: Failed to add character #{character.ID} to repository: {ex.Message}");
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

            try
            {
                repository.AgeUpBirthdays(curMonth, curDay);
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Warn("Safety", $"Birthday aging failed for {curMonth}/{curDay}: {ex.Message}");
            }

            foreach (var id in mortality.SelectDailyDeaths())
            {
                Kill(id, "Daily hazard");
            }

            bus.Publish(metrics.ToEvent(curYear, curMonth, curDay));
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            LogInfo($"Year {e.Year} begins. Alive: {repository.AliveCount}, Families: {repository.FamilyCount}");
        }

        public Character Get(int id) => repository.Get(id);

        public IReadOnlyList<Character> GetAllLiving() => repository.GetAllLiving();

        public IReadOnlyList<Character> GetByFamily(string gens) => repository.GetByFamily(gens);

        public IReadOnlyList<Character> GetByClass(SocialClass c) => repository.GetByClass(c);

        public int CountAlive() => repository.AliveCount;

        public int GetLiveCharacterCount() => repository.AliveCount;

        public int GetFamilyCount() => repository.FamilyCount;

        internal bool TryGetRepository(out CharacterRepository repo)
        {
            repo = repository;
            return repo != null;
        }

        public void AddCharacter(Character character)
        {
            repository.Add(character, config.KeepDeadInMemory);
            metrics.RecordBirth();
        }

        public bool Kill(int id, string cause = "Natural causes")
        {
            var character = repository.MarkDead(id, config.KeepDeadInMemory);
            if (character == null) return false;

            metrics.RecordDeath();
            bus.Publish(new OnCharacterDied(curYear, curMonth, curDay, id, cause));
            return true;
        }

        public bool Marry(int a, int b)
        {
            var spouseA = repository.Get(a);
            var spouseB = repository.Get(b);

            if (spouseA == null || spouseB == null)
                return false;

            if (!spouseA.IsAlive || !spouseB.IsAlive)
                return false;

            if (spouseA.SpouseID.HasValue || spouseB.SpouseID.HasValue)
                return false;

            spouseA.SpouseID = spouseB.ID;
            spouseB.SpouseID = spouseA.ID;

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
                    Seed = config.RngSeed,
                    Characters = repository.AllCharacters.Select(c => c).ToList(),
                    AliveIDs = repository.AliveIdSnapshot().ToList(),
                    DeadIDs = config.KeepDeadInMemory ? repository.DeadIdSnapshot().ToList() : new List<int>()
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
                    repository.Add(character, config.KeepDeadInMemory);

                repository.ApplyLifeState(blob.AliveIDs, blob.DeadIDs, config.KeepDeadInMemory);

                config.RngSeed = blob.Seed;
                rng = new System.Random(config.RngSeed);
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
            if (!config.Mortality.UseAgeBandHazards) return 0f;
            foreach (var band in config.Mortality.AgeBands)
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
