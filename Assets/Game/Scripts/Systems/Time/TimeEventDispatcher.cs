using System;
using Game.Systems.EventBus;

namespace Game.Systems.Time
{
    /// <summary>
    /// Publishes calendar-related events to the global event bus.
    /// </summary>
    public class TimeEventDispatcher
    {
        private readonly EventBus.EventBus eventBus;

        public TimeEventDispatcher(EventBus.EventBus eventBus)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void RaiseNewMonth(int year, int month, int day)
        {
            eventBus.Publish(new OnNewMonthEvent(year, month, day));
        }

        public void RaiseNewYear(int year, int month, int day)
        {
            eventBus.Publish(new OnNewYearEvent(year, month, day));
        }

        public void RaiseNewDay(int year, int month, int day)
        {
            eventBus.Publish(new OnNewDayEvent(year, month, day));
        }
    }
}
