using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public class SystemRegistry
    {
        private readonly List<IGameSystem> systems = new();
        private readonly Dictionary<Type, IGameSystem> systemLookup = new();
        private readonly List<SystemDescriptor> descriptors = new();

        public void RegisterDescriptor(SystemDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (descriptors.Any(d => d.SystemType == descriptor.SystemType) || systemLookup.ContainsKey(descriptor.SystemType))
            {
                Logger.Warn("SystemRegistry", $"Descriptor for system '{descriptor.SystemType.Name}' is already registered.");
                return;
            }

            descriptors.Add(descriptor);
        }

        public void RegisterSystem(IGameSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            RegisterSystemInternal(system);
        }

        private void RegisterSystemInternal(IGameSystem system)
        {
            var type = system.GetType();
            if (systemLookup.ContainsKey(type))
            {
                Logger.Warn("SystemRegistry", $"System '{type.Name}' already registered");
                return;
            }

            systems.Add(system);
            systemLookup[type] = system;
        }

        public void InitializeAll(GameState state)
        {
            MaterializeDescriptors();

            foreach (var system in systems)
            {
                foreach (var dep in system.Dependencies)
                {
                    bool exists = systems.Any(s => s.GetType() == dep);
                    if (!exists)
                    {
                        Logger.Warn("SystemRegistry",
                            $"{system.Name} missing dependency {dep.Name}. It may not initialize correctly.");
                    }
                }

                Logger.Info("SystemRegistry", $"Initializing {system.Name}...");
                system.Initialize(state);
            }
        }

        private void MaterializeDescriptors()
        {
            if (descriptors.Count == 0) return;

            var pending = descriptors.ToList();
            bool progress;

            do
            {
                progress = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    var descriptor = pending[i];

                    if (systemLookup.ContainsKey(descriptor.SystemType))
                    {
                        pending.RemoveAt(i);
                        progress = true;
                        continue;
                    }

                    bool dependenciesReady = descriptor.Dependencies.All(dep => systemLookup.ContainsKey(dep));
                    if (!dependenciesReady)
                        continue;

                    try
                    {
                        var resolver = new SystemResolver(type => systemLookup.TryGetValue(type, out var sys) ? sys : null);
                        var system = descriptor.Create(resolver);
                        RegisterSystemInternal(system);
                        pending.RemoveAt(i);
                        progress = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("SystemRegistry", $"Failed to create system {descriptor.SystemType.Name}: {ex.Message}");
                        pending.RemoveAt(i);
                        progress = true;
                    }
                }
            } while (progress && pending.Count > 0);

            foreach (var descriptor in pending)
            {
                var missing = descriptor.Dependencies
                    .Where(dep => !systemLookup.ContainsKey(dep))
                    .Select(dep => dep.Name);
                Logger.Error("SystemRegistry",
                    $"Unable to instantiate {descriptor.SystemType.Name}: unresolved dependencies [{string.Join(", ", missing)}].");
            }
        }

        public void UpdateAll(GameState state)
        {
            foreach (var system in systems)
            {
                system.Update(state);
            }
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            systemLookup.TryGetValue(typeof(T), out IGameSystem system);
            return system as T;
        }

        public void ShutdownAll()
        {
            foreach (var system in systems)
            {
                try
                {
                    system.Shutdown();
                }
                catch (Exception ex)
                {
                    Logger.Error("SystemRegistry", $"Error shutting down {system.Name}: {ex.Message}");
                }
            }

            systems.Clear();
            systemLookup.Clear();
            Logger.Info("SystemRegistry", "All systems shut down and registry cleared.");
        }

        public List<string> GetRegisteredSystemNames()
        {
            return systems.Select(s => s.Name).ToList();
        }

        public Dictionary<string, object> SaveAll()
        {
            var data = new Dictionary<string, object>();
            foreach (var system in systems)
            {
                string key = system.GetType().FullName;
                data[key] = system.Save();
            }
            return data;
        }

        public void LoadAll(Dictionary<string, object> data)
        {
            if (data == null) return;

            foreach (var system in systems)
            {
                string key = system.GetType().FullName;
                if (data.TryGetValue(key, out var sysData))
                {
                    if (sysData is Dictionary<string, object> dict)
                    {
                        system.Load(dict);
                    }
                    else
                    {
                        Logger.Warn("SystemRegistry", $"Save blob for {key} was not a Dictionary<string, object>. System skipped.");
                    }
                }
                else
                {
                    Logger.Warn("SystemRegistry", $"No save blob found for {key}. System will use defaults.");
                }
            }
        }
    }
}
