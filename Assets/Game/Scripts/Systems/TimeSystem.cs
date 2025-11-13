using Game.Core;
using Game.Systems.EventBus;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.TimeSystem
{
    public class TimeSystem : GameSystemBase
    {
        public override string Name => "Time System";
        public override IEnumerable<Type> Dependencies => new[] { typeof(EventBus.EventBus) };

        private readonly EventBus.EventBus eventBus;

        private int day;
        private int month;
        private int year;
        private float elapsedRealTime;
        private float secondsPerDay = 2f;
        private float speedMultiplier = 1f;
        public bool IsPaused { get; private set; }

        private static readonly int[] DaysInMonth =
            {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
        private static readonly string[] MonthNames =
            {"January", "February", "March", "April", "May", "June",
             "July", "August", "September", "October", "November", "December"};

        public TimeSystem(EventBus.EventBus eventBus)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            day = 1;
            month = 1;
            year = -248;
            LogInfo($"Initialized: {GetDateString()}");
        }

        public override void Update(GameState state)
        {
            if (!IsActive || IsPaused) return;

            elapsedRealTime += Time.deltaTime * speedMultiplier;
            if (elapsedRealTime >= secondsPerDay)
            {
                elapsedRealTime -= secondsPerDay;
                AdvanceDay();
            }
        }

        public void SetGameSpeed(float multiplier) =>
            speedMultiplier = Mathf.Max(0f, multiplier);

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

        public void SetSecondsPerDay(float seconds) =>
            secondsPerDay = Mathf.Max(0.1f, seconds);

        public (int Year, int Month, int Day) CurrentDate => (year, month, day);

        public void StepDays(int days)
        {
            if (days < 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Days must be non-negative.");

            for (int i = 0; i < days; i++)
                AdvanceDay();
        }

        private void AdvanceDay()
        {
            day++;

            if (day > DaysInMonth[month - 1])
            {
                day = 1;
                month++;
                eventBus.Publish(new OnNewMonthEvent(year, month, day));

                if (month > 12)
                {
                    month = 1;
                    year++;
                    if (year == 0) year = 1;

                    eventBus.Publish(new OnNewYearEvent(year, month, day));
                }
            }

            eventBus.Publish(new OnNewDayEvent(year, month, day));
            Log($"{GetDateString()}");
        }

        public override Dictionary<string, object> Save() => new()
        {
            ["day"] = day,
            ["month"] = month,
            ["year"] = year
        };

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null) return;

            try
            {
                if (data.TryGetValue("day", out var d)) day = Convert.ToInt32(d);
                if (data.TryGetValue("month", out var m)) month = Convert.ToInt32(m);
                if (data.TryGetValue("year", out var y)) year = Convert.ToInt32(y);

                elapsedRealTime = 0f;
                LogInfo($"Loaded date: {GetDateString()}");
            }
            catch (Exception ex)
            {
                LogError($"Load failed: {ex.Message}");
            }
        }

        private string GetDateString()
        {
            string suffix = year < 0 ? "BC" : "AD";
            int absYear = Math.Abs(year);
            string monthName = MonthNames[month - 1];
            return $"{monthName} {day}, {absYear} {suffix}";
        }

        public string GetCurrentDateString() => GetDateString();

        public (int year, int month, int day) GetCurrentDate() => (year, month, day);
    }
}
