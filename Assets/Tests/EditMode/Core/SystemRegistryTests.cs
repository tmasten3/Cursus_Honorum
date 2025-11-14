using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace CursusHonorum.Tests.Core
{
    public class SystemRegistryTests
    {
        [Test]
        public void SystemsInitializeAccordingToDependencies()
        {
            var registry = new SystemRegistry();
            var initializationOrder = new List<string>();

            registry.RegisterSystem(new GammaSystem(initializationOrder));
            registry.RegisterSystem(new AlphaSystem(initializationOrder));
            registry.RegisterSystem(new BetaSystem(initializationOrder));

            var state = new GameState(new SystemBootstrapProfile());

            registry.InitializeAll(state);

            CollectionAssert.AreEqual(new[] { "Alpha", "Beta", "Gamma" }, initializationOrder);
        }

        [Test]
        public void CyclesThrowInvalidOperationException()
        {
            var registry = new SystemRegistry();
            var log = new List<string>();

            registry.RegisterSystem(new CycleAlphaSystem(log));
            registry.RegisterSystem(new CycleBetaSystem(log));

            var state = new GameState(new SystemBootstrapProfile());

            Assert.That(() => registry.InitializeAll(state),
                Throws.InvalidOperationException.With.Message.Contains("Cycle detected"));
        }

        private abstract class RecordingSystem : GameSystemBase
        {
            private readonly List<string> log;
            private readonly string systemName;
            private readonly Type[] dependencies;

            protected RecordingSystem(string systemName, List<string> log, params Type[] dependencies)
            {
                this.systemName = systemName;
                this.log = log ?? throw new ArgumentNullException(nameof(log));
                this.dependencies = dependencies ?? Array.Empty<Type>();
            }

            public override string Name => systemName;

            public override IEnumerable<Type> Dependencies => dependencies;

            public override void Initialize(GameState state)
            {
                base.Initialize(state);
                log.Add(systemName);
            }

            protected override void OnTick(GameState state, float deltaTime) { }

            public override void Shutdown() { }

            public override Dictionary<string, object> Save()
            {
                return new Dictionary<string, object>();
            }

            public override void Load(Dictionary<string, object> data) { }
        }

        private sealed class AlphaSystem : RecordingSystem
        {
            public AlphaSystem(List<string> log) : base("Alpha", log)
            {
            }
        }

        private sealed class BetaSystem : RecordingSystem
        {
            public BetaSystem(List<string> log) : base("Beta", log, typeof(AlphaSystem))
            {
            }
        }

        private sealed class GammaSystem : RecordingSystem
        {
            public GammaSystem(List<string> log) : base("Gamma", log, typeof(BetaSystem))
            {
            }
        }

        private sealed class CycleAlphaSystem : RecordingSystem
        {
            public CycleAlphaSystem(List<string> log) : base("CycleAlpha", log, typeof(CycleBetaSystem))
            {
            }
        }

        private sealed class CycleBetaSystem : RecordingSystem
        {
            public CycleBetaSystem(List<string> log) : base("CycleBeta", log, typeof(CycleAlphaSystem))
            {
            }
        }
    }
}
