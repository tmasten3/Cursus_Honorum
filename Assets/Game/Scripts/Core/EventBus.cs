using System;
using System.Collections.Generic;
using Game.Core;
using Game.Systems.Politics.Elections;

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
        private readonly Dictionary<Type, List<Action<GameEvent>>> subscribers = new();
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

            var eventType = typeof(T);

            if (!subscribers.ContainsKey(eventType))
                subscribers[eventType] = new List<Action<GameEvent>>();

            subscribers[eventType].Add(e => handler((T)e));
            unhandledTypesLogged.Remove(eventType);
            LogInfo($"Subscribed to {eventType.Name}");
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
                history.Add(e);

                var eventType = e.GetType();

                if (subscribers.TryGetValue(eventType, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try { handler(e); }
                        catch (Exception ex)
                        {
                            LogError($"Error handling event {e.Name}: {ex.Message}");
                        }
                    }
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
                ["historyCount"] = history.Count
            };
        }

        public override void Load(Dictionary<string, object> data)
        {
            if (data == null) return;
            if (data.TryGetValue("historyCount", out var count))
                LogInfo($"Loaded event history with {count} entries.");
        }
    }
}
