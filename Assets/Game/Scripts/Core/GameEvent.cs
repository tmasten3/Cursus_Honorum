using System;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Categorises events routed through the global event bus.
    /// </summary>
    public enum EventCategory
    {
        Time,
        Character,
        Family,
        Influence,
        Office,
        Election,
        Senate,
        UI,
        Debug
    }

    /// <summary>
    /// Marker interface implemented by all events dispatched through the EventBus.
    /// </summary>
    public interface IGameEvent
    {
        string Name { get; }
        EventCategory Category { get; }
    }

    /// <summary>
    /// Base class for all timestamped simulation events.
    /// Provides shared properties and simple diagnostics.
    /// </summary>
    public abstract class GameEvent : IGameEvent
    {
        public string Name { get; }
        public EventCategory Category { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        protected GameEvent(string name, EventCategory category, int year, int month, int day)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Category = category;
            Year = year;
            Month = month;
            Day = day;
        }

        public override string ToString()
        {
            return $"{Name} ({Category}) @ {Year:D4}-{Month:D2}-{Day:D2}";
        }
    }

    /// <summary>
    /// Published when the simulation advances to a new day.
    /// </summary>
    public sealed class OnNewDayEvent : GameEvent
    {
        public OnNewDayEvent(int year, int month, int day)
            : base(nameof(OnNewDayEvent), EventCategory.Time, year, month, day) { }
    }

    /// <summary>
    /// Published when the simulation advances to a new month.
    /// </summary>
    public sealed class OnNewMonthEvent : GameEvent
    {
        public OnNewMonthEvent(int year, int month, int day)
            : base(nameof(OnNewMonthEvent), EventCategory.Time, year, month, day) { }
    }

    /// <summary>
    /// Published when the simulation advances to a new year.
    /// </summary>
    public sealed class OnNewYearEvent : GameEvent
    {
        public OnNewYearEvent(int year, int month, int day)
            : base(nameof(OnNewYearEvent), EventCategory.Time, year, month, day) { }
    }
}
