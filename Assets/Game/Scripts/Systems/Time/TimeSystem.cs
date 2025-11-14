using System;
using System.Collections.Generic;
using Game.Core;
using Game.Systems.EventBus;
using UnityEngine;

namespace Game.Systems.Time
{
    public class TimeSystem : GameSystemBase
    {
        public override string Name => "Time System";
        public override IEnumerable<Type> Dependencies => new[] { typeof(EventBus.EventBus) };

        private readonly TimeConfiguration configuration;
        private readonly TimeState state;
        private readonly TimeEventDispatcher dispatcher;

        private float elapsedRealTime;
        private float speedMultiplier = 1f;

        public bool IsPaused { get; private set; }

        public TimeSystem(EventBus.EventBus eventBus)
        {
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));

            configuration = new TimeConfiguration();
            state = new TimeState(configuration);
            dispatcher = new TimeEventDispatcher(eventBus);
        }

        public override void Initialize(GameState gameState)
        {
            base.Initialize(gameState);
            state.Initialize(-248, 1, 1);
            elapsedRealTime = 0f;
            LogInfo($"Initialized: {state.GetDateString()}");
        }

        protected override void OnTick(GameState gameState, float deltaTime)
        {
            if (IsPaused)
                return;

            if (deltaTime <= 0f)
                return;

            elapsedRealTime += deltaTime * speedMultiplier;
            while (elapsedRealTime >= configuration.SecondsPerDay)
            {
                elapsedRealTime -= configuration.SecondsPerDay;
                AdvanceDay();
            }
        }

        public void SetGameSpeed(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void Pause()
        {
            IsPaused = true;
            LogInfo("Simulation paused.");
        }

        public void Resume()
        {
            IsPaused = false;
            LogInfo("Simulation resumed.");
        }

        public void SetSecondsPerDay(float seconds)
        {
            configuration.SetSecondsPerDay(seconds);
        }

        public (int Year, int Month, int Day) CurrentDate => state.CurrentDate;

        public void StepDays(int days)
        {
            if (days < 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Days must be non-negative.");

            for (int i = 0; i < days; i++)
                AdvanceDay();
        }

        private void AdvanceDay()
        {
            var transition = state.IncrementDay();

            if (transition.IsNewMonth)
            {
                dispatcher.RaiseNewMonth(transition.YearForNewMonth, transition.NewMonthValue, state.Day);

                if (transition.IsNewYear)
                {
                    dispatcher.RaiseNewYear(transition.YearForNewYear, state.Month, state.Day);
                }
            }

            dispatcher.RaiseNewDay(state.Year, state.Month, state.Day);
            Log(state.GetDateString());
        }

        public override Dictionary<string, object> Save() => new()
        {
            ["day"] = state.Day,
            ["month"] = state.Month,
            ["year"] = state.Year
        };

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null)
                return;

            try
            {
                int newDay = state.Day;
                int newMonth = state.Month;
                int newYear = state.Year;

                if (data.TryGetValue("day", out var d)) newDay = Convert.ToInt32(d);
                if (data.TryGetValue("month", out var m)) newMonth = Convert.ToInt32(m);
                if (data.TryGetValue("year", out var y)) newYear = Convert.ToInt32(y);

                state.SetDate(newYear, newMonth, newDay);
                elapsedRealTime = 0f;
                LogInfo($"Loaded date: {state.GetDateString()}");
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        public string GetCurrentDateString() => state.GetDateString();

        public (int year, int month, int day) GetCurrentDate() => state.CurrentDate;
    }
}
