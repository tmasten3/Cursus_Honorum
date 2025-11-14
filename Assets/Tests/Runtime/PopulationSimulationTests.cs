using System.IO;
using Game.Core;
using Game.Data.Characters;
using NUnit.Framework;
using Game.Systems.EventBus;
using Game.Systems.Time;
using Game.Systems.CharacterSystem;
using Game.Systems.MarriageSystem;
using Game.Systems.BirthSystem;
using UnityEngine;
using Game.Systems.Population;

namespace CursusHonorum.Tests.Runtime
{
    public class PopulationSimulationTests
    {
        [Test]
        public void CharactersMarryAndChildrenAreBornOverExtendedTimeline()
        {
            var testConfig = new PopulationSimulationConfig
            {
                Birth = new BirthSettings
                {
                    RngSeed = 7331,
                    FemaleMinAge = 14,
                    FemaleMaxAge = 35,
                    DailyBirthChanceIfMarried = 0.0015f,
                    GestationDays = 270,
                    MultipleBirthChance = 0.02f
                },
                Marriage = new MarriageSettings
                {
                    RngSeed = 8221,
                    MinAgeMale = 14,
                    MinAgeFemale = 12,
                    DailyMatchmakingCap = 10,
                    DailyMarriageChanceWhenEligible = 0.002f,
                    PreferSameClassWeight = 1.5f,
                    CrossClassAllowed = true
                }
            };
            var (eventBus, _, characterSystem, simulationConfig, configPath) = CreatePopulationSimulationHarness(testConfig);

            var marriageSystem = new MarriageSystem(eventBus, characterSystem, simulationConfig)
            {
                ConfigPath = configPath
            };
            marriageSystem.Initialize(null);

            var birthSystem = new BirthSystem(eventBus, characterSystem, simulationConfig)
            {
                ConfigPath = configPath
            };
            birthSystem.Initialize(null);

            int marriageEvents = 0;
            int birthEvents = 0;

            eventBus.Subscribe<OnCharacterMarried>(_ => marriageEvents++);
            eventBus.Subscribe<OnCharacterBorn>(_ => birthEvents++);

            var date = new SimulationDate(-248, 1, 1);
            const int totalDays = 20000;

            for (int i = 0; i < totalDays; i++)
            {
                date = date.NextDay(out bool rolledMonth, out bool rolledYear);

                if (rolledMonth)
                    eventBus.Publish(new OnNewMonthEvent(date.Year, date.Month, date.Day));
                if (rolledYear)
                    eventBus.Publish(new OnNewYearEvent(date.Year, date.Month, date.Day));

                eventBus.Publish(new OnNewDayEvent(date.Year, date.Month, date.Day));
                eventBus.Update(null);
            }

            Assert.That(marriageEvents, Is.GreaterThan(0), "No marriages occurred during the extended simulation window.");
            Assert.That(birthEvents, Is.GreaterThan(0), "No births occurred during the extended simulation window.");
        }

        [Test]
        public void MarriageSystemAppliesPopulationSimulationConfigOverrides()
        {
            var overrideConfig = new PopulationSimulationConfig
            {
                Marriage = new MarriageSettings
                {
                    RngSeed = 9876,
                    MinAgeMale = 32,
                    MinAgeFemale = 28,
                    DailyMatchmakingCap = 3,
                    DailyMarriageChanceWhenEligible = 0.42f,
                    PreferSameClassWeight = 2.25f,
                    CrossClassAllowed = false
                }
            };

            var (eventBus, _, characterSystem, simulationConfig, configPath) = CreatePopulationSimulationHarness(overrideConfig);

            var marriageSystem = new MarriageSystem(eventBus, characterSystem, simulationConfig)
            {
                ConfigPath = configPath
            };
            marriageSystem.Initialize(null);

            Assert.Multiple(() =>
            {
                Assert.That(simulationConfig.Marriage.RngSeed, Is.EqualTo(overrideConfig.Marriage.RngSeed));
                Assert.That(simulationConfig.Marriage.MinAgeMale, Is.EqualTo(overrideConfig.Marriage.MinAgeMale));
                Assert.That(simulationConfig.Marriage.MinAgeFemale, Is.EqualTo(overrideConfig.Marriage.MinAgeFemale));
                Assert.That(simulationConfig.Marriage.DailyMatchmakingCap, Is.EqualTo(overrideConfig.Marriage.DailyMatchmakingCap));
                Assert.That(simulationConfig.Marriage.DailyMarriageChanceWhenEligible, Is.EqualTo(overrideConfig.Marriage.DailyMarriageChanceWhenEligible));
                Assert.That(simulationConfig.Marriage.PreferSameClassWeight, Is.EqualTo(overrideConfig.Marriage.PreferSameClassWeight));
                Assert.That(simulationConfig.Marriage.CrossClassAllowed, Is.EqualTo(overrideConfig.Marriage.CrossClassAllowed));
            });
        }

        [Test]
        public void BirthSystemAppliesPopulationSimulationConfigOverrides()
        {
            var overrideConfig = new PopulationSimulationConfig
            {
                Birth = new BirthSettings
                {
                    RngSeed = 2468,
                    FemaleMinAge = 20,
                    FemaleMaxAge = 27,
                    DailyBirthChanceIfMarried = 0.75f,
                    GestationDays = 200,
                    MultipleBirthChance = 0.33f
                }
            };

            var (eventBus, _, characterSystem, simulationConfig, configPath) = CreatePopulationSimulationHarness(overrideConfig);

            var birthSystem = new BirthSystem(eventBus, characterSystem, simulationConfig)
            {
                ConfigPath = configPath
            };
            birthSystem.Initialize(null);

            Assert.Multiple(() =>
            {
                Assert.That(simulationConfig.Birth.RngSeed, Is.EqualTo(overrideConfig.Birth.RngSeed));
                Assert.That(simulationConfig.Birth.FemaleMinAge, Is.EqualTo(overrideConfig.Birth.FemaleMinAge));
                Assert.That(simulationConfig.Birth.FemaleMaxAge, Is.EqualTo(overrideConfig.Birth.FemaleMaxAge));
                Assert.That(simulationConfig.Birth.DailyBirthChanceIfMarried, Is.EqualTo(overrideConfig.Birth.DailyBirthChanceIfMarried));
                Assert.That(simulationConfig.Birth.GestationDays, Is.EqualTo(overrideConfig.Birth.GestationDays));
                Assert.That(simulationConfig.Birth.MultipleBirthChance, Is.EqualTo(overrideConfig.Birth.MultipleBirthChance));
            });
        }

        private static string GetProjectRoot()
        {
            var testDir = TestContext.CurrentContext.TestDirectory;
            return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        }

        private static (EventBus EventBus, TimeSystem TimeSystem, CharacterSystem CharacterSystem, SimulationConfig SimulationConfig, string ConfigPath)
            CreatePopulationSimulationHarness(PopulationSimulationConfig config)
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            var tempDataPath = Path.Combine(Path.GetTempPath(), "CursusHonorumTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempDataPath);
            Application.persistentDataPath = tempDataPath;

            var configPath = Path.Combine(tempDataPath, "population_simulation.json");
            File.WriteAllText(configPath, JsonUtility.ToJson(config ?? new PopulationSimulationConfig()));

            var eventBus = new EventBus();
            eventBus.Initialize(null);

            var timeSystem = new TimeSystem(eventBus);
            timeSystem.Initialize(null);

            var simulationConfig = SimulationConfigLoader.LoadOrDefault();

            var characterSystem = new CharacterSystem(eventBus, timeSystem, simulationConfig);
            characterSystem.Initialize(null);

            return (eventBus, timeSystem, characterSystem, simulationConfig, configPath);
        }

        private readonly record struct SimulationDate(int Year, int Month, int Day)
        {
            public SimulationDate NextDay(out bool rolledMonth, out bool rolledYear)
            {
                var (year, month, day) = CalendarUtility.AddDays(Year, Month, Day, 1);

                rolledMonth = month != Month;
                rolledYear = year != Year;

                return new SimulationDate(year, month, day);
            }
        }
    }
}
