using System;

namespace Game.Systems.Time
{
    /// <summary>
    /// Tracks and mutates the current in-game calendar date.
    /// </summary>
    public class TimeState
    {
        private readonly TimeConfiguration configuration;

        private int day;
        private int month;
        private int year;

        public TimeState(TimeConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public int Day => day;
        public int Month => month;
        public int Year => year;

        public (int Year, int Month, int Day) CurrentDate => (year, month, day);

        public void Initialize(int initialYear, int initialMonth, int initialDay)
        {
            year = initialYear;
            month = initialMonth;
            day = initialDay;
        }

        public void SetDate(int newYear, int newMonth, int newDay)
        {
            year = newYear;
            month = newMonth;
            day = newDay;
        }

        public TimeAdvanceResult IncrementDay()
        {
            int yearBeforeIncrement = year;
            day++;

            if (day <= configuration.DaysInMonth[month - 1])
            {
                return TimeAdvanceResult.None;
            }

            day = 1;
            bool newYear = IncrementMonth(out int monthValue);
            int yearForNewYear = year;

            return new TimeAdvanceResult(true, monthValue, yearBeforeIncrement, newYear, yearForNewYear);
        }

        public bool IncrementMonth(out int monthValue)
        {
            month++;
            monthValue = month;

            if (month <= configuration.MonthsInYear)
            {
                return false;
            }

            month = 1;
            IncrementYear();
            return true;
        }

        public void IncrementYear()
        {
            year++;
            if (year == 0)
            {
                year = 1;
            }
        }

        public string GetDateString()
        {
            string suffix = year < 0 ? "BC" : "AD";
            int absYear = Math.Abs(year);
            string monthName = configuration.MonthNames[month - 1];
            return $"{monthName} {day}, {absYear} {suffix}";
        }
    }

    public readonly struct TimeAdvanceResult
    {
        public static readonly TimeAdvanceResult None = new(false, 0, 0, false, 0);

        public TimeAdvanceResult(bool isNewMonth, int newMonthValue, int yearForNewMonth, bool isNewYear, int yearForNewYear)
        {
            IsNewMonth = isNewMonth;
            NewMonthValue = newMonthValue;
            YearForNewMonth = yearForNewMonth;
            IsNewYear = isNewYear;
            YearForNewYear = yearForNewYear;
        }

        public bool IsNewMonth { get; }
        public int NewMonthValue { get; }
        public int YearForNewMonth { get; }
        public bool IsNewYear { get; }
        public int YearForNewYear { get; }
    }
}
