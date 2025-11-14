using System;
using System.Collections;
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

        public const int DefaultHistoryCapacity = 4096;

        private readonly Queue<GameEvent> currentQueue = new();
        private readonly Queue<GameEvent> nextQueue = new();
        private readonly EventHistory history;
        private readonly HashSet<Type> unhandledTypesLogged = new();
        private static readonly HashSet<Type> optionalEventTypes = new()
        {
            typeof(OnCharacterMarried),
            typeof(OnPopulationTick),
            typeof(OnNewMonthEvent),
            typeof(ElectionSeasonOpenedEvent),
            typeof(ElectionSeasonCompletedEvent),
            typeof(Game.Core.Save.OnGameSavedEvent),
            typeof(Game.Core.Save.OnGameLoadedEvent)
        };

        private readonly EventRegistry registry = new();
        private readonly EventInvoker invoker = new();
        private int historyCapacity = DefaultHistoryCapacity;
        private bool isFlushing;

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

        public EventBus(int historyCapacity = DefaultHistoryCapacity)
        {
            history = new EventHistory(Math.Max(0, historyCapacity));
            HistoryCapacity = historyCapacity;
        }

        public int HistoryCount => history.Count;

        public IReadOnlyList<GameEvent> GetHistorySnapshot()
        {
            return history.ToArray();
        }

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

            if (!isFlushing)
                FlushEvents();
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
            if (isFlushing)
                return;

            isFlushing = true;

            try
            {
                while (currentQueue.Count > 0 || nextQueue.Count > 0)
                {
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
            }
            finally
            {
                isFlushing = false;
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
        }

        private void EnforceHistoryCapacity()
        {
            history.SetCapacity(historyCapacity);
        }

        private sealed class EventHistory : IReadOnlyList<GameEvent>
        {
            private GameEvent[] buffer;
            private int start;
            private int count;

            public EventHistory(int capacity)
            {
                if (capacity < 0)
                    capacity = 0;

                buffer = capacity == 0 ? Array.Empty<GameEvent>() : new GameEvent[capacity];
                start = 0;
                count = 0;
                Capacity = capacity;
            }

            public int Capacity { get; private set; }

            public int Count => count;

            public GameEvent this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)count)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    int actualIndex = (start + index) % buffer.Length;
                    return buffer[actualIndex];
                }
            }

            public void Add(GameEvent e)
            {
                if (Capacity == 0)
                    return;

                if (buffer.Length != Capacity)
                    buffer = new GameEvent[Capacity];

                if (count < Capacity)
                {
                    int index = (start + count) % Capacity;
                    buffer[index] = e;
                    count++;
                }
                else
                {
                    buffer[start] = e;
                    start = (start + 1) % Capacity;
                }
            }

            public void SetCapacity(int capacity)
            {
                if (capacity < 0)
                    capacity = 0;

                if (capacity == Capacity)
                    return;

                if (capacity == 0)
                {
                    buffer = Array.Empty<GameEvent>();
                    Capacity = 0;
                    start = 0;
                    count = 0;
                    return;
                }

                var newBuffer = new GameEvent[capacity];
                int itemsToCopy = Math.Min(count, capacity);

                for (int i = 0; i < itemsToCopy; i++)
                {
                    newBuffer[i] = this[count - itemsToCopy + i];
                }

                buffer = newBuffer;
                Capacity = capacity;
                start = 0;
                count = itemsToCopy;
            }

            public GameEvent[] ToArray()
            {
                if (count == 0)
                    return Array.Empty<GameEvent>();

                var result = new GameEvent[count];
                for (int i = 0; i < count; i++)
                    result[i] = this[i];

                return result;
            }

            public IEnumerator<GameEvent> GetEnumerator()
            {
                for (int i = 0; i < count; i++)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
