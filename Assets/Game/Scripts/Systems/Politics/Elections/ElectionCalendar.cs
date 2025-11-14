using System;
using Game.Systems.EventBus;
using Game.Systems.Time;

namespace Game.Systems.Politics.Elections
{
    public class ElectionCalendar
    {
        private readonly EventBus.EventBus eventBus;
        private readonly TimeSystem timeSystem;
        private bool subscriptionsActive;

        public int CurrentYear { get; private set; }
        public int CurrentMonth { get; private set; }
        public int CurrentDay { get; private set; }

        public event Action<OnNewYearEvent> YearStarted;
        public event Action<OnNewDayEvent> DeclarationWindowOpened;
        public event Action<OnNewDayEvent> ElectionDayArrived;

        public ElectionCalendar(EventBus.EventBus eventBus, TimeSystem timeSystem)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
        }

        public void Initialize()
        {
            if (subscriptionsActive)
                return;

            eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Subscribe<OnNewYearEvent>(OnNewYear);
            subscriptionsActive = true;

            var (year, month, day) = timeSystem.GetCurrentDate();
            UpdateDate(year, month, day);
        }

        public void Shutdown()
        {
            if (!subscriptionsActive)
                return;

            eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Unsubscribe<OnNewYearEvent>(OnNewYear);
            subscriptionsActive = false;
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            UpdateDate(e.Year, e.Month, e.Day);
            YearStarted?.Invoke(e);
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            UpdateDate(e.Year, e.Month, e.Day);
            if (e.Month == 6 && e.Day == 1)
            {
                DeclarationWindowOpened?.Invoke(e);
            }
            else if (e.Month == 7 && e.Day == 1)
            {
                ElectionDayArrived?.Invoke(e);
            }
        }

        private void UpdateDate(int year, int month, int day)
        {
            CurrentYear = year;
            CurrentMonth = month;
            CurrentDay = day;
        }
    }
}
