using System;
using System.Collections.Generic;

namespace Game.Core
{
    public class GameState
    {
        private readonly SystemRegistry registry = new();
        private readonly SystemBootstrapProfile profile;

        public GameState(SystemBootstrapProfile profile = null)
        {
            this.profile = profile ?? SystemBootstrapProfile.Default;
        }

        public void Initialize()
        {
            foreach (var descriptor in profile.Descriptors)
                registry.RegisterDescriptor(descriptor);

            registry.InitializeAll(this);

            var names = registry.GetRegisteredSystemNames();
            Logger.Info("GameState", $"Initialized with {names.Count} systems: {string.Join(", ", names)}.");
        }

        public void Update()
        {
            registry.UpdateAll(this);
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            return registry.GetSystem<T>();
        }

        public object SaveGame()
        {
            return registry.SaveAll();
        }

        public void LoadGame(object data)
        {
            if (data is Dictionary<string, object> dict)
            {
                registry.LoadAll(dict);
                Logger.Info("GameState", "Save loaded into systems.");
            }
            else
            {
                Logger.Warn("GameState", "Invalid save object passed to LoadGame().");
            }
        }

        public void Shutdown() => registry.ShutdownAll();

    }
}
