using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Describes how the simulation should instantiate and wire game systems.
    /// </summary>
    public sealed class SystemBootstrapProfile
    {
        private readonly List<SystemDescriptor> descriptors;

        public IReadOnlyList<SystemDescriptor> Descriptors => descriptors;

        public SystemBootstrapProfile() : this(Array.Empty<SystemDescriptor>())
        {
        }

        public SystemBootstrapProfile(IEnumerable<SystemDescriptor> descriptors)
        {
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));
            this.descriptors = descriptors.ToList();
        }

        public static SystemBootstrapProfile Default => CreateDefaultProfile();

        public static SystemBootstrapProfile CreateDefaultProfile()
        {
            var profile = new SystemBootstrapProfile();
            profile.LoadDefaultSystems();
            return profile;
        }

        public void LoadDefaultSystems()
        {
            descriptors.Clear();

            AddSystem(_ => new Game.Systems.EventBus.EventBus());
            AddSystem(
                resolver => new Game.Systems.Time.TimeSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>()),
                typeof(Game.Systems.EventBus.EventBus));
            AddSystem(
                resolver => new Game.Systems.CharacterSystem.CharacterSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>(),
                    resolver.Resolve<Game.Systems.Time.TimeSystem>()),
                typeof(Game.Systems.EventBus.EventBus),
                typeof(Game.Systems.Time.TimeSystem));
            AddSystem(
                resolver => new Game.Systems.BirthSystem.BirthSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>(),
                    resolver.Resolve<Game.Systems.CharacterSystem.CharacterSystem>()),
                typeof(Game.Systems.EventBus.EventBus),
                typeof(Game.Systems.CharacterSystem.CharacterSystem));
            AddSystem(
                resolver => new Game.Systems.Politics.Offices.OfficeSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>(),
                    resolver.Resolve<Game.Systems.CharacterSystem.CharacterSystem>()),
                typeof(Game.Systems.EventBus.EventBus),
                typeof(Game.Systems.CharacterSystem.CharacterSystem));
            AddSystem(
                resolver => new Game.Systems.MarriageSystem.MarriageSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>(),
                    resolver.Resolve<Game.Systems.CharacterSystem.CharacterSystem>()),
                typeof(Game.Systems.EventBus.EventBus),
                typeof(Game.Systems.CharacterSystem.CharacterSystem));
            AddSystem(
                resolver => new Game.Systems.Politics.Elections.ElectionSystem(
                    resolver.Resolve<Game.Systems.EventBus.EventBus>(),
                    resolver.Resolve<Game.Systems.Time.TimeSystem>(),
                    resolver.Resolve<Game.Systems.CharacterSystem.CharacterSystem>(),
                    resolver.Resolve<Game.Systems.Politics.Offices.OfficeSystem>()),
                typeof(Game.Systems.EventBus.EventBus),
                typeof(Game.Systems.Time.TimeSystem),
                typeof(Game.Systems.CharacterSystem.CharacterSystem),
                typeof(Game.Systems.Politics.Offices.OfficeSystem));
        }

        public void AddSystem<TSystem>(Func<SystemResolver, TSystem> factory, params Type[] dependencies)
            where TSystem : class, IGameSystem
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            descriptors.Add(SystemDescriptor.For(factory, dependencies));
        }
    }

    /// <summary>
    /// Factory descriptor used by the registry to instantiate a system and wire dependencies.
    /// </summary>
    public sealed class SystemDescriptor
    {
        private readonly Func<SystemResolver, IGameSystem> factory;

        public Type SystemType { get; }
        public IReadOnlyCollection<Type> Dependencies { get; }

        public SystemDescriptor(Type systemType, Func<SystemResolver, IGameSystem> factory, IEnumerable<Type> dependencies = null)
        {
            SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Dependencies = dependencies?.Distinct().ToArray() ?? Array.Empty<Type>();
        }

        public static SystemDescriptor For<TSystem>(Func<SystemResolver, TSystem> factory, IEnumerable<Type> dependencies = null)
            where TSystem : class, IGameSystem
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return new SystemDescriptor(typeof(TSystem), resolver => factory(resolver), dependencies);
        }

        internal IGameSystem Create(SystemResolver resolver)
        {
            var system = factory(resolver);
            if (system == null)
                throw new InvalidOperationException($"System factory for {SystemType.Name} returned null.");
            return system;
        }
    }

    /// <summary>
    /// Lightweight resolver exposed to factories so they can request dependencies.
    /// </summary>
    public sealed class SystemResolver
    {
        private readonly Func<Type, IGameSystem> accessor;

        internal SystemResolver(Func<Type, IGameSystem> accessor)
        {
            accessor ??= _ => null;
            this.accessor = accessor;
        }

        public T Resolve<T>() where T : class, IGameSystem
        {
            var type = typeof(T);
            var system = accessor(type) as T;
            if (system == null)
                throw new InvalidOperationException($"System '{type.Name}' is not available for injection.");
            return system;
        }

        public bool TryResolve<T>(out T system) where T : class, IGameSystem
        {
            system = accessor(typeof(T)) as T;
            return system != null;
        }
    }
}
