using System;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Disposable subscription token returned from the EventBus for easy unsubscription.
    /// </summary>
    public sealed class EventSubscription : IDisposable
    {
        public static EventSubscription Empty { get; } = new EventSubscription();

        private readonly EventBus owner;
        private readonly Type eventType;
        private readonly Delegate handler;
        private bool isDisposed;

        internal EventSubscription(EventBus owner, Type eventType, Delegate handler)
        {
            this.owner = owner;
            this.eventType = eventType;
            this.handler = handler;
        }

        private EventSubscription()
        {
            isDisposed = true;
        }

        public bool IsActive => !isDisposed && owner != null;

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            owner?.Unsubscribe(eventType, handler);
        }
    }
}
