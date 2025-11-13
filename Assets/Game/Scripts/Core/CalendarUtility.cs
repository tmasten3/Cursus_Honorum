using System;

namespace Game.Core
{
    /// <summary>
    /// Provides helper methods for working with the simulation calendar.
    /// </summary>
    public static class CalendarUtility
    {
        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>
        /// Adds the specified number of days to the given date using the simulation calendar rules.
        /// </summary>
        /// <param name="year">The starting year.</param>
        /// <param name="month">The starting month (1-12).</param>
        /// <param name="day">The starting day (1-31 depending on month).</param>
        /// <param name="days">The number of days to add. Must be non-negative.</param>
        /// <returns>The resulting date after adding the specified days.</returns>
        public static (int Year, int Month, int Day) AddDays(int year, int month, int day, int days)
        {
            if (days < 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Days must be non-negative.");

            int newYear = year;
            int newMonth = month;
            int newDay = day;

            while (days-- > 0)
            {
                newDay++;
                if (newDay > DaysInMonth[newMonth - 1])
                {
                    newDay = 1;
                    newMonth++;

                    if (newMonth > 12)
                    {
                        newMonth = 1;
                        newYear++;

                        if (newYear == 0)
                        {
                            newYear = 1;
                        }
                    }
                }
            }

            return (newYear, newMonth, newDay);
        }
    }
}
