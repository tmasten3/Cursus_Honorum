using System;
using System.Collections.Generic;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Time;
using UnityEngine;

namespace Game.UI.CharacterDetail
{
    public static class CharacterSelection
    {
        private static EventBus eventBus;
        private static TimeSystem timeSystem;
        private static bool attemptedResolve;
        private static readonly Dictionary<Action<CharacterSelectedEvent>, Action<CharacterSelectedEventEnvelope>> subscriberLookup = new();
        private static readonly object syncRoot = new();

        public static void Bind(EventBus bus, TimeSystem time)
        {
            eventBus = bus;
            timeSystem = time;
            attemptedResolve = true;
        }

        public static void Subscribe(EventBus bus, Action<CharacterSelectedEvent> handler)
        {
            if (bus == null || handler == null)
                return;

            Action<CharacterSelectedEventEnvelope> wrapper;

            lock (syncRoot)
            {
                if (!subscriberLookup.TryGetValue(handler, out wrapper))
                {
                    wrapper = envelope => handler(envelope.Payload);
                    subscriberLookup[handler] = wrapper;
                }
            }

            bus.Subscribe(wrapper);
        }

        public static void Unsubscribe(EventBus bus, Action<CharacterSelectedEvent> handler)
        {
            if (bus == null || handler == null)
                return;

            lock (syncRoot)
            {
                if (!subscriberLookup.TryGetValue(handler, out var wrapper))
                    return;

                bus.Unsubscribe(wrapper);
                subscriberLookup.Remove(handler);
            }
        }

        public static void SelectCharacter(int id)
        {
            if (id <= 0)
                return;

            if (!EnsureDependencies())
                return;

            var (year, month, day) = timeSystem != null ? timeSystem.GetCurrentDate() : (0, 0, 0);
            var payload = new CharacterSelectedEvent { CharacterId = id };
            eventBus.Publish(new CharacterSelectedEventEnvelope(year, month, day, payload));
        }

        private static bool EnsureDependencies()
        {
            if (eventBus != null)
                return true;

            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null)
            {
                if (!attemptedResolve)
                    Logger.Warn("CharacterSelection", "GameController not found when attempting character selection.");
                attemptedResolve = true;
                return false;
            }

            var state = controller.GameState;
            if (state == null)
            {
                if (!attemptedResolve)
                    Logger.Warn("CharacterSelection", "GameState not initialized when attempting character selection.");
                attemptedResolve = true;
                return false;
            }

            eventBus = state.GetSystem<EventBus>();
            timeSystem ??= state.GetSystem<TimeSystem>();

            if (eventBus == null && !attemptedResolve)
                Logger.Warn("CharacterSelection", "EventBus not available when attempting character selection.");

            attemptedResolve = true;
            return eventBus != null;
        }
    }
}
