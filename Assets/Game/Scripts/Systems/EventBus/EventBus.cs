using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Centralized publish/subscribe bus used by modular game systems to communicate.
    /// </summary>
    public class EventBus : GameSystemBase
    {
        public override string Name => "Event Bus";

        private readonly Queue<GameEvent> currentQueue = new();
        private readonly Queue<GameEvent> nextQueue = new();
        private readonly List<GameEvent> history = new();
        private readonly HashSet<Type> unhandledTypesLogged = new();
        private static readonly HashSet<Type> optionalEventTypes = new()
        {
            typeof(OnCharacterMarried),
            typeof(OnPopulationTick),
            typeof(OnNewMonthEvent),
            typeof(ElectionSeasonOpenedEvent),
            typeof(ElectionSeasonCompletedEvent)
        };

        private readonly EventRegistry registry = new();
        private readonly EventInvoker invoker = new();
        private int historyCapacity = DefaultHistoryCapacity;

        public const int DefaultHistoryCapacity = 4096;

        public int HistoryCapacity
        {
            get => historyCapacity;
            set
            {
                historyCapacity = Math.Max(0, value);
                EnforceHistoryCapacity();
            }
        }

        public IReadOnlyList<GameEvent> History => history;

        public override void Initialize(GameState state)
        {
            base.Initialize(state);
            LogInfo("Initialized event bus and subscriber registry.");
        }

        public override void Update(GameState state)
        {
            if (!IsActive) return;
            FlushEvents();
        }

        public void Publish(GameEvent e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            nextQueue.Enqueue(e);
        }

        public void Subscribe<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!registry.TryAddSubscriber(handler, out bool isDuplicate))
            {
                if (isDuplicate)
                    LogWarn($"Duplicate subscription to {typeof(T).Name} ignored.");
                return;
            }

            unhandledTypesLogged.Remove(typeof(T));
            LogInfo($"Subscribed to {typeof(T).Name}");
        }

        public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null)
                return;

            if (!registry.TryRemoveSubscriber(handler))
                return;

            LogInfo($"Unsubscribed from {typeof(T).Name}");
        }

        private void FlushEvents()
        {
            if (currentQueue.Count == 0 && nextQueue.Count == 0)
                return;

            while (nextQueue.Count > 0)
                currentQueue.Enqueue(nextQueue.Dequeue());

            while (currentQueue.Count > 0)
            {
                var e = currentQueue.Dequeue();
                AddToHistory(e);

                var eventType = e.GetType();
                var handlers = registry.GetHandlers(eventType);

                if (handlers.Count > 0)
                {
                    invoker.Invoke(e, handlers, ex => LogError($"Error handling event {e.Name}: {ex.Message}"));
                }
                else if (unhandledTypesLogged.Add(eventType))
                {
                    if (optionalEventTypes.Contains(eventType))
                        LogInfo($"No subscribers for {e.Name} (optional event)");
                    else
                        LogWarn($"No subscribers for {e.Name}");
                }
            }
        }

        public override Dictionary<string, object> Save()
        {
            return new Dictionary<string, object>
            {
                ["pending"] = nextQueue.Count + currentQueue.Count,
                ["historyCount"] = history.Count,
                ["historyCapacity"] = historyCapacity
            };
        }

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null) return;
            if (data.TryGetValue("historyCount", out var count))
                LogInfo($"Loaded event history with {count} entries.");
            if (data.TryGetValue("historyCapacity", out var capacity) && capacity is int storedCapacity)
                HistoryCapacity = storedCapacity;
        }

        private void AddToHistory(GameEvent e)
        {
            if (historyCapacity == 0 || e == null)
                return;

            history.Add(e);
            EnforceHistoryCapacity();
        }

        private void EnforceHistoryCapacity()
        {
            if (historyCapacity <= 0)
            {
                if (history.Count > 0)
                    history.Clear();
                return;
            }

            int overflow = history.Count - historyCapacity;
            if (overflow <= 0)
                return;

            history.RemoveRange(0, overflow);
        }
    }
}
