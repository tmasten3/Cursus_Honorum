using System;
using System.Collections.Generic;
using Game.Core;
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
        private System.Random rng;
        private bool subscriptionsActive;

        private MarriageSettings config = new();

        public string ConfigPath { get; set; } = PopulationSimulationConfigLoader.DefaultConfigPath;

        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(EventBus.EventBus), typeof(CharacterSystem.CharacterSystem) };

        public MarriageSystem(EventBus.EventBus bus, CharacterSystem.CharacterSystem characterSystem)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
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

            rng = new System.Random(config.RngSeed);
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

            base.Shutdown();
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            int marriagesToday = 0;

            var singlesMale = new List<Character>();
            var singlesFemale = new List<Character>();

            foreach (var c in characterSystem.GetAllLiving())
            {
                if (c.SpouseID.HasValue) continue;

                if (c.Gender == Gender.Male && c.Age >= config.MinAgeMale)
                    singlesMale.Add(c);
                else if (c.Gender == Gender.Female && c.Age >= config.MinAgeFemale)
                    singlesFemale.Add(c);
            }

            int attempts = 0;
            while (attempts < config.DailyMatchmakingCap && singlesMale.Count > 0 && singlesFemale.Count > 0)
            {
                int mIndex = rng.Next(singlesMale.Count);
                var male = singlesMale[mIndex];

                int fIndex = WeightedPickFemale(singlesFemale, male.Class);
                var female = singlesFemale[fIndex];

                if (rng.NextDouble() < config.DailyMarriageChanceWhenEligible)
                {
                    if (characterSystem.Marry(male.ID, female.ID))
                    {
                        marriagesToday++;

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
                    w *= config.PreferSameClassWeight;
                if (!config.CrossClassAllowed && f.Class != maleClass)
                    w = 0.0;

                weights[i] = w;
                total += w;
            }

            if (total <= 0) return rng.Next(females.Count);

            double roll = rng.NextDouble() * total;
            double acc = 0;
            for (int i = 0; i < females.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return i;
            }
            return females.Count - 1;
        }
    }
}
