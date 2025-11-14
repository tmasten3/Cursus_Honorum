using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using Game.Core.Save;
using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class SaveSerializerTests
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

        [Test]
        public void Roundtrip_GameState_ProducesEquivalentValues()
        {
            var serializer = new SaveSerializer(StubRequiredKeys);
            var baseline = CreateStubState();
            var restored = CreateStubState();

            try
            {
                baseline.GetSystem<StubTimeSystem>().SetValue(42);
                baseline.GetSystem<StubCharacterSystem>().SetValue(17);
                baseline.GetSystem<StubOfficeSystem>().SetValue(9);
                baseline.GetSystem<StubElectionSystem>().SetValue(23);
                baseline.GetSystem<StubBirthSystem>().SetValue(5);
                baseline.GetSystem<StubMarriageSystem>().SetValue(11);

                var expectedValues = CaptureValues(baseline);

                var saveData = serializer.CreateSaveData(baseline);
                var validation = serializer.Validate(saveData);
                Assert.That(validation.IsValid, Is.True, "Baseline save data should validate.");

                string json = serializer.Serialize(saveData);
                Assert.That(string.IsNullOrWhiteSpace(json), Is.False, "Serialized JSON should not be empty.");

                var roundtrip = serializer.Deserialize(json);
                serializer.ApplyToGameState(restored, roundtrip);

                var restoredValues = CaptureValues(restored);
                CollectionAssert.AreEquivalent(expectedValues, restoredValues);
            }
            finally
            {
                baseline.Shutdown();
                restored.Shutdown();
            }
        }

        [Test]
        public void Validate_DetectsMissingRequiredSystem()
        {
            var serializer = new SaveSerializer(StubRequiredKeys);
            var saveData = new SaveData
            {
                Version = SaveSerializer.CurrentVersion,
                TimestampUtc = DateTime.UtcNow,
                Systems = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [StubRequiredKeys[0]] = new Dictionary<string, object>()
                }
            };

            var result = serializer.Validate(saveData);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains(StubRequiredKeys[1])), Is.True,
                "Validation should report missing system entries.");
        }

        [Test]
        public void Serialize_ProducesJsonWithCoreFields()
        {
            var serializer = new SaveSerializer(StubRequiredKeys);
            var timestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var saveData = new SaveData
            {
                Version = SaveSerializer.CurrentVersion,
                TimestampUtc = timestamp,
                Systems = StubRequiredKeys.ToDictionary(k => k, _ => (object)new Dictionary<string, object>(), StringComparer.Ordinal)
            };

            string json = serializer.Serialize(saveData);
            Assert.That(json, Does.Contain("\"version\""));
            Assert.That(json, Does.Contain("\"timestamp\""));
            Assert.That(json, Does.Contain(timestamp.ToString("o", CultureInfo.InvariantCulture)));
            foreach (var key in StubRequiredKeys)
                Assert.That(json, Does.Contain(key));
        }

        [Test]
        public void Deserialize_HandlesPartialDataAndSignalsErrors()
        {
            var serializer = new SaveSerializer(StubRequiredKeys);
            string json = $"{{\"version\":1,\"timestamp\":\"2025-01-01T00:00:00Z\",\"state\":{{\"{StubRequiredKeys[0]}\":{{}}}}}}";

            var data = serializer.Deserialize(json);
            Assert.That(data.Version, Is.EqualTo(1));

            var result = serializer.Validate(data);
            Assert.That(result.IsValid, Is.False, "Missing systems should invalidate the payload.");
            Assert.That(result.Errors.Any(), Is.True);
        }

        [Test]
        public void Validate_FlagsVersionMismatchAsWarning()
        {
            var serializer = new SaveSerializer(StubRequiredKeys);
            var saveData = new SaveData
            {
                Version = SaveSerializer.CurrentVersion + 5,
                TimestampUtc = DateTime.UtcNow,
                Systems = StubRequiredKeys.ToDictionary(k => k, _ => (object)new Dictionary<string, object>(), StringComparer.Ordinal)
            };

            var result = serializer.Validate(saveData);
            Assert.That(result.IsValid, Is.True, "Version mismatch should not block loading by itself.");
            Assert.That(result.Warnings.Any(w => w.Contains("differs")), Is.True);
        }

        private static GameState CreateStubState()
        {
            var descriptors = new[]
            {
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

            protected override void OnTick(GameState state, float deltaTime)
            {
                // No-op for stub systems in tests
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
