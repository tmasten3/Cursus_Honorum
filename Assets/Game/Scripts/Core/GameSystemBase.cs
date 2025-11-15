using System;
using System.Collections.Generic;
using Game.Systems.EventBus;

namespace Game.Core
{
    /// <summary>
    /// Base class for all modular game systems.
    /// Provides shared functionality, defaults, and global helpers.
    /// </summary>
    public abstract class GameSystemBase : IGameSystem
    {
        // --------------------------------------------------------------------
        // 🔹 Identity & Dependencies
        // --------------------------------------------------------------------
        public virtual string Name => GetType().Name;

        public virtual IEnumerable<Type> Dependencies => Array.Empty<Type>();

        // --------------------------------------------------------------------
        // 🔹 Shared Core State
        // --------------------------------------------------------------------
        /// <summary>
        /// Reference to the active GameState, assigned on Initialize().
        /// Lets all systems access other systems without extra parameters.
        /// </summary>
        protected GameState State { get; private set; }

        /// <summary>
        /// Whether this system is currently active and should update.
        /// </summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// Whether Initialize() has been called successfully.
        /// </summary>
        public bool IsInitialized { get; private set; }

        // --------------------------------------------------------------------
        // 🔹 Lifecycle
        // --------------------------------------------------------------------
        public virtual void Initialize(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (IsInitialized)
                throw new InvalidOperationException($"{Name} has already been initialized.");

            State = state; // store GameState for shared access
            IsActive = true;
            IsInitialized = true;
            LogInfo("Initialized (base)");
        }

        public void Tick(GameState state, float deltaTime)
        {
            if (!IsInitialized)
                return;

            if (!IsActive)
                return;

            var context = state ?? State;
            if (context == null)
                return;

            var safeDeltaTime = deltaTime < 0f ? 0f : deltaTime;

            OnTick(context, safeDeltaTime);
        }

        protected virtual void OnTick(GameState state, float deltaTime)
        {
            // Default systems do not require per-frame ticking.
        }

        public virtual void Shutdown()
        {
            if (!IsInitialized)
                return;

            LogInfo("Shutdown (base)");
            IsActive = false;
            IsInitialized = false;
            State = null;
        }


        // --------------------------------------------------------------------
        // 🔹 Activation Control
        // --------------------------------------------------------------------
        public virtual void Activate()
        {
            if (!IsActive)
            {
                IsActive = true;
                LogInfo("Activated");
            }
        }

        public virtual void Deactivate()
        {
            if (IsActive)
            {
                IsActive = false;
                LogInfo("Deactivated");
            }
        }

        // --------------------------------------------------------------------
        // 🔹 Persistence (Safe Defaults)
        // --------------------------------------------------------------------
        public virtual Dictionary<string, object> Save() => new Dictionary<string, object>();

        public virtual void Load(Dictionary<string, object> data)
        {
            // Safe default: do nothing
        }

        public virtual void OnEvent<TEvent>(TEvent evt) where TEvent : IGameEvent
        {
            // Optional override point for systems that consume routed events directly.
        }

        // --------------------------------------------------------------------
        // 🔹 Logging Helpers
        // --------------------------------------------------------------------
        protected void Log(string message) => Logger.Log(Name, message);
        protected void LogInfo(string message) => Logger.Info(Name, message);
        protected void LogWarn(string message) => Logger.Warn(Name, message);
        protected void LogError(string message) => Logger.Error(Name, message);
    }
}
