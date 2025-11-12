using System;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Base class for all events routed through the EventBus.
    /// Provides a common timestamp payload for downstream consumers.
    /// </summary>
    public abstract class GameEvent
    {
        public string Name { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        protected GameEvent(string name, int year, int month, int day)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Year = year;
            Month = month;
            Day = day;
        }
    }

    /// <summary>
    /// Published when the simulation advances to a new day.
    /// </summary>
    public sealed class OnNewDayEvent : GameEvent
    {
        public OnNewDayEvent(int year, int month, int day)
            : base("OnNewDay", year, month, day) { }
    }

    /// <summary>
    /// Published when the simulation advances to a new month.
    /// </summary>
    public sealed class OnNewMonthEvent : GameEvent
    {
        public OnNewMonthEvent(int year, int month, int day)
            : base("OnNewMonth", year, month, day) { }
    }

    /// <summary>
    /// Published when the simulation advances to a new year.
    /// </summary>
    public sealed class OnNewYearEvent : GameEvent
    {
        public OnNewYearEvent(int year, int month, int day)
            : base("OnNewYear", year, month, day) { }
    }
}
