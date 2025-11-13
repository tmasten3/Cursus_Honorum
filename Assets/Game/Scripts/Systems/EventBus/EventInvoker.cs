using System;
using System.Collections.Generic;

namespace Game.Systems.EventBus
{
    /// <summary>
    /// Handles invoking event handlers with safety and ordering guarantees.
    /// </summary>
    public class EventInvoker
    {
        public void Invoke(GameEvent eventData, IReadOnlyList<Action<GameEvent>> handlers, Action<Exception> onHandlerException)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            if (handlers.Count == 0) return;

            var snapshot = new Action<GameEvent>[handlers.Count];
            for (int i = 0; i < handlers.Count; i++)
                snapshot[i] = handlers[i];

            foreach (var handler in snapshot)
            {
                try
                {
                    handler(eventData);
                }
                catch (Exception ex)
                {
                    onHandlerException?.Invoke(ex);
                }
            }
        }
    }
}
