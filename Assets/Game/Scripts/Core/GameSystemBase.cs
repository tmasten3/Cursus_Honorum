using System;
using System.Collections.Generic;

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

        // --------------------------------------------------------------------
        // 🔹 Lifecycle
        // --------------------------------------------------------------------
        public virtual void Initialize(GameState state)
        {
            State = state; // store GameState for shared access
            LogInfo("Initialized (base)");
        }

        public abstract void Update(GameState state);

        public virtual void Shutdown()
        {
            LogInfo("Shutdown (base)");
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
        public virtual Dictionary<string, object> Save() => new();

        public virtual void Load(Dictionary<string, object> data)
        {
            // Safe default: do nothing
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
