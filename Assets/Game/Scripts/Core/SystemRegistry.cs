using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public class SystemRegistry
    {
        private readonly List<GameSystemBase> systems = new();
        private readonly Dictionary<Type, GameSystemBase> systemLookup = new();
        private readonly Dictionary<Type, int> registrationOrder = new();
        private int registrationSequence;
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

        public void RegisterSystem(GameSystemBase system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            RegisterSystemInternal(system);
        }

        private void RegisterSystemInternal(GameSystemBase system)
        {
            var type = system.GetType();
            if (systemLookup.ContainsKey(type))
            {
                Logger.Warn("SystemRegistry", $"System '{type.Name}' already registered");
                return;
            }

            systems.Add(system);
            systemLookup[type] = system;
            registrationOrder[type] = registrationSequence++;
        }

        public void InitializeAll(GameState state)
        {
            MaterializeDescriptors();

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (systems.Count == 0)
                return;

            var orderedSystems = BuildInitializationOrder();

            systems.Clear();
            systems.AddRange(orderedSystems);

            foreach (var system in systems)
            {
                Logger.Info("SystemRegistry", $"Initializing {system.Name}...");
                try
                {
                    system.Initialize(state);
                }
                catch (Exception ex)
                {
                    Logger.Error("SystemRegistry", $"Initialization failed for {system.Name}: {ex.Message}");
                    throw;
                }
            }
        }

        private List<GameSystemBase> BuildInitializationOrder()
        {
            var typeLookup = systems.ToDictionary(s => s.GetType());
            var indegree = new Dictionary<Type, int>();
            var adjacency = new Dictionary<Type, List<Type>>();

            foreach (var system in systems)
            {
                var type = system.GetType();
                indegree[type] = 0;
                adjacency[type] = new List<Type>();
            }

            foreach (var system in systems)
            {
                var type = system.GetType();
                var dependencies = (system.Dependencies ?? Enumerable.Empty<Type>())
                    .Where(dep => dep != null)
                    .Distinct();

                foreach (var dependency in dependencies)
                {
                    if (!typeLookup.ContainsKey(dependency))
                    {
                        string message = $"{system.Name} missing dependency {dependency.Name}.";
                        Logger.Error("SystemRegistry", message);
                        throw new InvalidOperationException(message);
                    }

                    adjacency[dependency].Add(type);
                    indegree[type] = indegree[type] + 1;
                }
            }

            var ordered = new List<GameSystemBase>(systems.Count);
            var available = indegree
                .Where(kv => kv.Value == 0)
                .Select(kv => kv.Key)
                .OrderBy(GetRegistrationOrder)
                .ThenBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

            Comparison<Type> comparison = (x, y) =>
            {
                int orderCompare = GetRegistrationOrder(x).CompareTo(GetRegistrationOrder(y));
                if (orderCompare != 0)
                    return orderCompare;
                return string.Compare(x.FullName, y.FullName, StringComparison.Ordinal);
            };

            while (available.Count > 0)
            {
                var current = available[0];
                available.RemoveAt(0);
                ordered.Add(typeLookup[current]);

                foreach (var dependent in adjacency[current])
                {
                    indegree[dependent]--;
                    if (indegree[dependent] == 0)
                    {
                        available.Add(dependent);
                        available.Sort(comparison);
                    }
                }
            }

            if (ordered.Count != systems.Count)
            {
                var cycleTypes = indegree
                    .Where(kv => kv.Value > 0)
                    .Select(kv => kv.Key.Name)
                    .Distinct()
                    .OrderBy(name => name);

                string message = $"Cycle detected in system dependencies: {string.Join(", ", cycleTypes)}.";
                Logger.Error("SystemRegistry", message);
                throw new InvalidOperationException(message);
            }

            return ordered;
        }

        private int GetRegistrationOrder(Type type)
        {
            return type != null && registrationOrder.TryGetValue(type, out var order) ? order : int.MaxValue;
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

        public void TickAll(GameState state, float deltaTime)
        {
            foreach (var system in systems)
            {
                system.Tick(state, deltaTime);
            }
        }

        public T GetSystem<T>() where T : GameSystemBase
        {
            systemLookup.TryGetValue(typeof(T), out GameSystemBase system);
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
            registrationOrder.Clear();
            registrationSequence = 0;
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
