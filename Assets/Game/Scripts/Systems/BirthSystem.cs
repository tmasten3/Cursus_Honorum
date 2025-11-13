using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;

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
        private System.Random rng;
        private bool subscriptionsActive;

        [Serializable]
        private struct Pregnancy
        {
            public int MotherID;
            public int? FatherID;
            public int DueYear, DueMonth, DueDay;
        }

        private List<Pregnancy> pregnancies = new();

        [Serializable]
        private class Config
        {
            public int RngSeed = 1338;
            public int FemaleMinAge = 14;
            public int FemaleMaxAge = 35;
            public float DailyBirthChanceIfMarried = 0.0015f;
            public int GestationDays = 270;
            public float MultipleBirthChance = 0.02f;
        }
        private Config config = new();

        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(EventBus.EventBus), typeof(CharacterSystem.CharacterSystem) };

        public BirthSystem(EventBus.EventBus bus, CharacterSystem.CharacterSystem characterSystem)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.characterSystem = characterSystem ?? throw new ArgumentNullException(nameof(characterSystem));
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
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

            pregnancies.Clear();
            base.Shutdown();
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
                c.Age >= config.FemaleMinAge &&
                c.Age <= config.FemaleMaxAge))
            {
                if (pregnancies.Any(p => p.MotherID == mother.ID))
                    continue;

                if (rng.NextDouble() < config.DailyBirthChanceIfMarried)
                {
                    var father = characterSystem.Get(mother.SpouseID.Value);
                    var due = CalendarUtility.AddDays(year, month, day, config.GestationDays);

                    pregnancies.Add(new Pregnancy
                    {
                        MotherID = mother.ID,
                        FatherID = father?.ID,
                        DueYear = due.Y,
                        DueMonth = due.M,
                        DueDay = due.D
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

                if (rng.NextDouble() < config.MultipleBirthChance)
                {
                    var twin = CharacterFactory.CreateChild(father, mother, year, month, day);
                    characterSystem.AddCharacter(twin);
                    bus.Publish(new OnCharacterBorn(year, month, day, twin.ID, father?.ID, mother.ID));
                }
            }

            pregnancies.RemoveAll(p => p.DueYear == year && p.DueMonth == month && p.DueDay == day);
        }

    }
}
