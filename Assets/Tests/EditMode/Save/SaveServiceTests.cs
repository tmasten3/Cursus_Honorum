using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Core.Save;
using Game.Systems.EventBus;
using NUnit.Framework;

namespace CursusHonorum.Tests.Save
{
    public class SaveServiceTests
    {
        private static readonly string[] StubRequiredKeys =
        {
            typeof(StubTimeSystem).FullName,
            typeof(StubCharacterSystem).FullName,
            typeof(StubOfficeSystem).FullName,
            typeof(StubElectionSystem).FullName,
            typeof(StubBirthSystem).FullName,
            typeof(StubMarriageSystem).FullName
        };

        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "CursusHonorum_SaveServiceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // ignore cleanup issues
                }
            }
        }

        [Test]
        public void SaveAndLoad_RestoreStubValues()
        {
            var repository = new SaveRepository(tempRoot);
            var serializer = new SaveSerializer(StubRequiredKeys);

            var original = CreateStubState();
            var restored = CreateStubState();

            try
            {
                var saveService = new SaveService(original, repository, serializer);
                original.GetSystem<StubTimeSystem>().SetValue(3);
                original.GetSystem<StubCharacterSystem>().SetValue(7);
                original.GetSystem<StubOfficeSystem>().SetValue(11);
                original.GetSystem<StubElectionSystem>().SetValue(19);
                original.GetSystem<StubBirthSystem>().SetValue(23);
                original.GetSystem<StubMarriageSystem>().SetValue(29);

                var expectedValues = CaptureValues(original);

                var saveResult = saveService.SaveGame("slotA");
                Assert.That(saveResult.Success, Is.True, saveResult.ErrorMessage);
                original.Tick(0f);

                var originalBus = original.GetSystem<EventBus>();
                Assert.That(originalBus.History.OfType<OnGameSavedEvent>().Any(), Is.True, "Save event was not published.");

                var loadService = new SaveService(restored, repository, serializer);
                var loadResult = loadService.LoadGame("slotA");
                Assert.That(loadResult.Success, Is.True, loadResult.ErrorMessage);
                restored.Tick(0f);

                var restoredValues = CaptureValues(restored);
                CollectionAssert.AreEquivalent(expectedValues, restoredValues);

                var restoredBus = restored.GetSystem<EventBus>();
                Assert.That(restoredBus.History.OfType<OnGameLoadedEvent>().Any(), Is.True, "Load event was not published.");
            }
            finally
            {
                original.Shutdown();
                restored.Shutdown();
            }
        }

        [Test]
        public void Load_CorruptFile_ReturnsFailure()
        {
            var repository = new SaveRepository(tempRoot);
            var serializer = new SaveSerializer(StubRequiredKeys);
            var state = CreateStubState();

            try
            {
                var path = repository.GetSaveFilePath("broken");
                File.WriteAllText(path, "{{ not valid json", Encoding.UTF8);

                var service = new SaveService(state, repository, serializer);
                var result = service.LoadGame("broken");
                Assert.That(result.Success, Is.False);

                state.Tick(0f);
                var bus = state.GetSystem<EventBus>();
                Assert.That(bus.History.OfType<OnGameLoadedEvent>().Any(), Is.False);
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void ListAndDeleteSaves_WorkCorrectly()
        {
            var repository = new SaveRepository(tempRoot);
            var serializer = new SaveSerializer(StubRequiredKeys);
            var state = CreateStubState();

            try
            {
                var service = new SaveService(state, repository, serializer);
                state.GetSystem<StubTimeSystem>().SetValue(1);
                Assert.That(service.SaveGame("alpha").Success, Is.True);
                state.GetSystem<StubTimeSystem>().SetValue(2);
                Assert.That(service.SaveGame("beta").Success, Is.True);

                var saves = service.ListSaves();
                Assert.That(saves.Count, Is.EqualTo(2));
                CollectionAssert.AreEquivalent(new[] { "alpha", "beta" }, saves.Select(s => s.SlotName));

                Assert.That(service.DeleteSave("alpha"), Is.True);
                Assert.That(repository.SaveExists("alpha"), Is.False);

                var remaining = service.ListSaves();
                Assert.That(remaining.Count, Is.EqualTo(1));
                Assert.That(remaining[0].SlotName, Is.EqualTo("beta"));
            }
            finally
            {
                state.Shutdown();
            }
        }

        private static GameState CreateStubState()
        {
            var descriptors = new List<SystemDescriptor>
            {
                SystemDescriptor.For(_ => new EventBus()),
                SystemDescriptor.For(_ => new StubTimeSystem()),
                SystemDescriptor.For(_ => new StubCharacterSystem()),
                SystemDescriptor.For(_ => new StubOfficeSystem()),
                SystemDescriptor.For(_ => new StubElectionSystem()),
                SystemDescriptor.For(_ => new StubBirthSystem()),
                SystemDescriptor.For(_ => new StubMarriageSystem())
            };

            var profile = new SystemBootstrapProfile(descriptors);
            var state = new GameState(profile);
            state.Initialize();
            return state;
        }

        private static Dictionary<string, int> CaptureValues(GameState state)
        {
            return new Dictionary<string, int>
            {
                [typeof(StubTimeSystem).FullName] = state.GetSystem<StubTimeSystem>().Value,
                [typeof(StubCharacterSystem).FullName] = state.GetSystem<StubCharacterSystem>().Value,
                [typeof(StubOfficeSystem).FullName] = state.GetSystem<StubOfficeSystem>().Value,
                [typeof(StubElectionSystem).FullName] = state.GetSystem<StubElectionSystem>().Value,
                [typeof(StubBirthSystem).FullName] = state.GetSystem<StubBirthSystem>().Value,
                [typeof(StubMarriageSystem).FullName] = state.GetSystem<StubMarriageSystem>().Value
            };
        }

        private abstract class StubValueSystem : GameSystemBase
        {
            private readonly string name;
            private int value;

            protected StubValueSystem(string name)
            {
                this.name = name;
            }

            public override string Name => name;
            public int Value => value;

            public void SetValue(int newValue)
            {
                value = newValue;
            }

            protected override void OnTick(GameState state, float deltaTime)
            {
                // Stub systems do not simulate behaviour during tests.
            }

            public override Dictionary<string, object> Save() => new()
            {
                ["value"] = value
            };

            public override void Load(Dictionary<string, object> data)
            {
                if (data == null)
                    return;

                if (data.TryGetValue("value", out var raw))
                    value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
        }

        private sealed class StubTimeSystem : StubValueSystem
        {
            public StubTimeSystem() : base("Stub Time") { }
        }

        private sealed class StubCharacterSystem : StubValueSystem
        {
            public StubCharacterSystem() : base("Stub Characters") { }
        }

        private sealed class StubOfficeSystem : StubValueSystem
        {
            public StubOfficeSystem() : base("Stub Offices") { }
        }

        private sealed class StubElectionSystem : StubValueSystem
        {
            public StubElectionSystem() : base("Stub Elections") { }
        }

        private sealed class StubBirthSystem : StubValueSystem
        {
            public StubBirthSystem() : base("Stub Births") { }
        }

        private sealed class StubMarriageSystem : StubValueSystem
        {
            public StubMarriageSystem() : base("Stub Marriages") { }
        }
    }
}
