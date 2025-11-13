using System.Collections.Generic;

namespace Game.Systems.Time
{
    /// <summary>
    /// Provides configuration data for the in-game calendar and tick duration.
    /// </summary>
    public class TimeConfiguration
    {
        private const float MinimumSecondsPerDay = 0.1f;

        private readonly IReadOnlyList<int> daysInMonth = new[]
        {
            31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31
        };

        private readonly IReadOnlyList<string> monthNames = new[]
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        private float secondsPerDay = 2f;

        public IReadOnlyList<int> DaysInMonth => daysInMonth;
        public IReadOnlyList<string> MonthNames => monthNames;

        public int MonthsInYear => daysInMonth.Count;

        public float SecondsPerDay => secondsPerDay;

        public void SetSecondsPerDay(float seconds)
        {
            secondsPerDay = seconds < MinimumSecondsPerDay ? MinimumSecondsPerDay : seconds;
        }
    }
}
