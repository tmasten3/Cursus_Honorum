using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;

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
        private bool subscriptionsActive;

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

            rng = new System.Random(rngSeed);
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
                int mIndex = rng.Next(singlesMale.Count);
                var male = singlesMale[mIndex];

                int fIndex = WeightedPickFemale(singlesFemale, male.Class);
                var female = singlesFemale[fIndex];

                if (rng.NextDouble() < settings.DailyMarriageChanceWhenEligible)
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
