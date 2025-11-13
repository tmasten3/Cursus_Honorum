using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Population;

namespace Game.Systems.BirthSystem
{
    /// <summary>
    /// Handles pregnancies and births using data from CharacterSystem.
    /// Subscribes to OnNewDayEvent and schedules births based on fertility.
    /// </summary>
    public class BirthSystem : GameSystemBase
    {
        public override string Name => "Birth System";

        private readonly EventBus.EventBus bus;
        private readonly CharacterSystem.CharacterSystem characterSystem;
        private readonly SimulationConfig.BirthSettings settings;
        private System.Random rng;
        private int rngSeed;
        private int rngSampleCount;
        private bool subscriptionsActive;

        [Serializable]
        private struct Pregnancy
        {
            public int MotherID;
            public int? FatherID;
            public int DueYear, DueMonth, DueDay;
        }

        private List<Pregnancy> pregnancies = new();

        private BirthSettings config = new();

        public string ConfigPath { get; set; } = PopulationSimulationConfigLoader.DefaultConfigPath;

        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(EventBus.EventBus), typeof(CharacterSystem.CharacterSystem) };

        public BirthSystem(EventBus.EventBus bus, CharacterSystem.CharacterSystem characterSystem, SimulationConfig simulationConfig)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            if (simulationConfig == null) throw new ArgumentNullException(nameof(simulationConfig));

            settings = simulationConfig.Birth ?? throw new ArgumentNullException(nameof(simulationConfig.Birth));
            rngSeed = settings.RngSeed;
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);

            var loadedConfig = PopulationSimulationConfigLoader.Load(
                ConfigPath,
                LogInfo,
                LogWarn,
                LogError);

            config = (loadedConfig ?? new PopulationSimulationConfig()).Birth ?? new BirthSettings();

            rngSeed = config.RngSeed;
            RestoreRngState(rngSeed, 0);
            if (!subscriptionsActive)
            {
                bus.Subscribe<OnNewDayEvent>(OnNewDay);
                subscriptionsActive = true;
            }
            LogInfo("Initialized and subscribed to OnNewDayEvent.");
        }

        public override void Update(GameState state) { }

        public override void Shutdown()
        {
            if (subscriptionsActive)
            {
                bus.Unsubscribe<OnNewDayEvent>(OnNewDay);
                subscriptionsActive = false;
            }

            pregnancies.Clear();
            base.Shutdown();
        }

        public override Dictionary<string, object> Save()
        {
            try
            {
                var blob = new SaveBlob
                {
                    Seed = config.RngSeed,
                    SampleCount = rngSampleCount,
                    Pregnancies = new List<Pregnancy>(pregnancies)
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
                    LogWarn("No valid save data found for BirthSystem.");
                    return;
                }

                var blob = JsonUtility.FromJson<SaveBlob>(json);
                pregnancies = blob?.Pregnancies != null
                    ? new List<Pregnancy>(blob.Pregnancies)
                    : new List<Pregnancy>();

                int seed = blob?.Seed ?? config.RngSeed;
                int sampleCount = blob?.SampleCount ?? 0;
                config.RngSeed = seed;
                RestoreRngState(seed, sampleCount);
                LogInfo($"Loaded {pregnancies.Count} pending pregnancies.");
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            TrySchedulePregnancies(e.Year, e.Month, e.Day);
            ResolveDueBirths(e.Year, e.Month, e.Day);
        }

        private void TrySchedulePregnancies(int year, int month, int day)
        {
            foreach (var mother in characterSystem.GetAllLiving().Where(c =>
                c.Gender == Gender.Female &&
                c.SpouseID.HasValue &&
                c.Age >= settings.FemaleMinAge &&
                c.Age <= settings.FemaleMaxAge))
            {
                if (pregnancies.Any(p => p.MotherID == mother.ID))
                    continue;

                if (NextRandomDouble() < settings.DailyBirthChanceIfMarried)
                {
                    var father = characterSystem.Get(mother.SpouseID.Value);
                    var due = CalendarUtility.AddDays(year, month, day, settings.GestationDays);

                    pregnancies.Add(new Pregnancy
                    {
                        MotherID = mother.ID,
                        FatherID = father?.ID,
                        DueYear = due.Year,
                        DueMonth = due.Month,
                        DueDay = due.Day
                    });
                }
            }
        }

        private void ResolveDueBirths(int year, int month, int day)
        {
            var due = pregnancies.Where(p => p.DueYear == year && p.DueMonth == month && p.DueDay == day).ToList();
            if (due.Count == 0) return;

            foreach (var p in due.OrderBy(p => p.MotherID))
            {
                var mother = characterSystem.Get(p.MotherID);
                var father = p.FatherID.HasValue ? characterSystem.Get(p.FatherID.Value) : null;

                var child = CharacterFactory.CreateChild(father, mother, year, month, day);
                characterSystem.AddCharacter(child);
                bus.Publish(new OnCharacterBorn(year, month, day, child.ID, father?.ID, mother.ID));

                if (NextRandomDouble() < settings.MultipleBirthChance)
                {
                    var twin = CharacterFactory.CreateChild(father, mother, year, month, day);
                    characterSystem.AddCharacter(twin);
                    bus.Publish(new OnCharacterBorn(year, month, day, twin.ID, father?.ID, mother.ID));
                }
            }

            pregnancies.RemoveAll(p => p.DueYear == year && p.DueMonth == month && p.DueDay == day);
        }

        private double NextRandomDouble()
        {
            rngSampleCount++;
            return rng.NextDouble();
        }

        private void RestoreRngState(int seed, int sampleCount)
        {
            rng = new System.Random(seed);
            if (sampleCount > 0)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    _ = rng.Next();
                }
            }
            rngSampleCount = sampleCount;
        }

        [Serializable]
        private class SaveBlob
        {
            public int Version = 1;
            public int Seed;
            public int SampleCount;
            public List<Pregnancy> Pregnancies = new();
        }
    }
}
