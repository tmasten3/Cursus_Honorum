using System;
using System.Collections.Generic;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Maintains subscriber lists for each event type.
    /// </summary>
    public class EventRegistry
    {
        private readonly Dictionary<Type, List<Action<GameEvent>>> subscribers = new();
        private readonly Dictionary<Type, Dictionary<Delegate, Action<GameEvent>>> subscriberLookup = new();

        public bool TryAddSubscriber<T>(Action<T> handler, out bool isDuplicate) where T : GameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            if (!subscriberLookup.TryGetValue(eventType, out var typedLookup))
            {
                typedLookup = new Dictionary<Delegate, Action<GameEvent>>();
                subscriberLookup[eventType] = typedLookup;
            }

            if (typedLookup.ContainsKey(handler))
            {
                isDuplicate = true;
                return false;
            }

            Action<GameEvent> wrapper = e => handler((T)e);
            typedLookup[handler] = wrapper;

            if (!subscribers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new List<Action<GameEvent>>();
                subscribers[eventType] = handlerList;
            }

            handlerList.Add(wrapper);
            isDuplicate = false;
            return true;
        }

        public bool TryRemoveSubscriber<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null)
                return false;

            var eventType = typeof(T);

            if (!subscriberLookup.TryGetValue(eventType, out var typedLookup))
                return false;

            if (!typedLookup.TryGetValue(handler, out var wrapper))
                return false;

            typedLookup.Remove(handler);

            if (subscribers.TryGetValue(eventType, out var handlerList))
            {
                handlerList.Remove(wrapper);
                if (handlerList.Count == 0)
                {
                    subscribers.Remove(eventType);
                }
            }

            if (typedLookup.Count == 0)
            {
                subscriberLookup.Remove(eventType);
            }

            return true;
        }

        public IReadOnlyList<Action<GameEvent>> GetHandlers(Type eventType)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));

            if (subscribers.TryGetValue(eventType, out var handlers))
                return handlers;

            return Array.Empty<Action<GameEvent>>();
        }

        public bool HasHandlers(Type eventType)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));

            return subscribers.TryGetValue(eventType, out var handlers) && handlers.Count > 0;
        }
    }
}
