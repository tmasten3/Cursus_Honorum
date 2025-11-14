using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Population;

namespace Game.Systems.MarriageSystem
{
    /// <summary>
    /// Handles daily marriage pairing, eligibility, and alliance formation.
    /// Uses CharacterSystem for data operations and publishes marriage events.
    /// </summary>
    public class MarriageSystem : GameSystemBase
    {
        public override string Name => "Marriage System";

        private readonly EventBus.EventBus bus;
        private readonly CharacterSystem.CharacterSystem characterSystem;
        private readonly SimulationConfig.MarriageSettings settings;
        private System.Random rng;
        private int rngSeed;
        private int rngSampleCount;
        private EventSubscription newDaySubscription = EventSubscription.Empty;

        private MarriageSettings config = new();

        public string ConfigPath { get; set; } = PopulationSimulationConfigLoader.DefaultConfigPath;

        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(EventBus.EventBus), typeof(CharacterSystem.CharacterSystem) };

        public MarriageSystem(EventBus.EventBus bus, CharacterSystem.CharacterSystem characterSystem, SimulationConfig simulationConfig)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
            if (simulationConfig == null) throw new ArgumentNullException(nameof(simulationConfig));

            settings = simulationConfig.Marriage ?? throw new ArgumentNullException(nameof(simulationConfig.Marriage));
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

            config = (loadedConfig ?? new PopulationSimulationConfig()).Marriage ?? new MarriageSettings();
            ApplyLoadedConfig(config);

            ApplyConfigOverrides(config);

            rngSeed = settings.RngSeed;
            RestoreRngState(rngSeed, 0);
            newDaySubscription.Dispose();
            newDaySubscription = bus.Subscribe<OnNewDayEvent>(OnNewDay);
            LogInfo("Initialized and subscribed to OnNewDayEvent.");
        }

        public override void Update(GameState state) { }

        public override void Shutdown()
        {
            newDaySubscription.Dispose();
            newDaySubscription = EventSubscription.Empty;

            base.Shutdown();
        }

        public override Dictionary<string, object> Save()
        {
            try
            {
                config.RngSeed = settings.RngSeed;
                rngSeed = settings.RngSeed;
                settings.RngSeed = rngSeed;
                config.RngSeed = rngSeed;

                var blob = new SaveBlob
                {
                    Seed = rngSeed,
                    SampleCount = rngSampleCount
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
                    LogWarn("No valid save data found for MarriageSystem.");
                    return;
                }

                var blob = JsonUtility.FromJson<SaveBlob>(json);
                int seed = blob?.Seed ?? settings.RngSeed;
                int sampleCount = blob?.SampleCount ?? 0;
                settings.RngSeed = seed;
                config.RngSeed = seed;
                settings.RngSeed = seed;
                rngSeed = seed;
                RestoreRngState(seed, sampleCount);
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            int attempts = 0;

            var singlesMale = new List<Character>();
            var singlesFemale = new List<Character>();

            foreach (var c in characterSystem.GetAllLiving())
            {
                if (c.SpouseID.HasValue) continue;

                if (c.Gender == Gender.Male && c.Age >= settings.MinAgeMale)
                    singlesMale.Add(c);
                else if (c.Gender == Gender.Female && c.Age >= settings.MinAgeFemale)
                    singlesFemale.Add(c);
            }

            while (attempts < settings.DailyMatchmakingCap && singlesMale.Count > 0 && singlesFemale.Count > 0)
            {
                int mIndex = NextRandomInt(singlesMale.Count);
                var male = singlesMale[mIndex];

                int fIndex = WeightedPickFemale(singlesFemale, male.Class);
                var female = singlesFemale[fIndex];

                if (NextRandomDouble() < settings.DailyMarriageChanceWhenEligible)
                {
                    if (characterSystem.Marry(male.ID, female.ID))
                    {
                        bus.Publish(new OnCharacterMarried(e.Year, e.Month, e.Day, male.ID, female.ID));

                        singlesMale.RemoveAt(mIndex);
                        singlesFemale.RemoveAt(fIndex);
                    }
                }

                attempts++;
            }
        }

        private int WeightedPickFemale(List<Character> females, SocialClass maleClass)
        {
            double total = 0;
            double[] weights = new double[females.Count];

            for (int i = 0; i < females.Count; i++)
            {
                var f = females[i];
                double w = 1.0;
                if (f.Class == maleClass)
                    w *= settings.PreferSameClassWeight;
                if (!settings.CrossClassAllowed && f.Class != maleClass)
                    w = 0.0;

                weights[i] = w;
                total += w;
            }

            if (total <= 0) return NextRandomInt(females.Count);

            double roll = NextRandomDouble() * total;
            double acc = 0;
            for (int i = 0; i < females.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return i;
            }
            return females.Count - 1;
        }

        private void ApplyConfigOverrides(MarriageSettings overrides)
        {
            if (overrides == null)
                return;

            settings.RngSeed = overrides.RngSeed;
            settings.MinAgeMale = overrides.MinAgeMale;
            settings.MinAgeFemale = overrides.MinAgeFemale;
            settings.DailyMatchmakingCap = overrides.DailyMatchmakingCap;
            settings.DailyMarriageChanceWhenEligible = overrides.DailyMarriageChanceWhenEligible;
            settings.PreferSameClassWeight = overrides.PreferSameClassWeight;
            settings.CrossClassAllowed = overrides.CrossClassAllowed;
        }

        private double NextRandomDouble()
        {
            rngSampleCount++;
            return rng.NextDouble();
        }

        private int NextRandomInt(int maxExclusive)
        {
            rngSampleCount++;
            return rng.Next(maxExclusive);
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

        private void ApplyLoadedConfig(MarriageSettings source)
        {
            if (source == null)
                return;

            settings.RngSeed = source.RngSeed;
            settings.MinAgeMale = source.MinAgeMale;
            settings.MinAgeFemale = source.MinAgeFemale;
            settings.DailyMatchmakingCap = source.DailyMatchmakingCap;
            settings.DailyMarriageChanceWhenEligible = source.DailyMarriageChanceWhenEligible;
            settings.PreferSameClassWeight = source.PreferSameClassWeight;
            settings.CrossClassAllowed = source.CrossClassAllowed;
        }

        [Serializable]
        private class SaveBlob
        {
            public int Version = 1;
            public int Seed;
            public int SampleCount;
        }
    }
}
