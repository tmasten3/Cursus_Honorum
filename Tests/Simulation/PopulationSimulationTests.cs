using System.IO;
using Game.Core;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Time;
using Game.Systems.CharacterSystem;
using Game.Systems.MarriageSystem;
using Game.Systems.BirthSystem;
using UnityEngine;
using Game.Systems.Population;

namespace CursusHonorum.Tests.Simulation
{
    public class PopulationSimulationTests
    {
        [Test]
        public void CharactersMarryAndChildrenAreBornOverExtendedTimeline()
        {
            Directory.SetCurrentDirectory(GetProjectRoot());

            var tempDataPath = Path.Combine(Path.GetTempPath(), "CursusHonorumTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempDataPath);
            Application.persistentDataPath = tempDataPath;

            var configPath = Path.Combine(tempDataPath, "population_simulation.json");
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
            File.WriteAllText(configPath, JsonUtility.ToJson(testConfig));

            var eventBus = new EventBus();
            eventBus.Initialize(null);

            var timeSystem = new TimeSystem(eventBus);
            timeSystem.Initialize(null);

            var simulationConfig = SimulationConfigLoader.LoadOrDefault();

            var characterSystem = new CharacterSystem(eventBus, timeSystem, simulationConfig);
            characterSystem.Initialize(null);

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

        private static string GetProjectRoot()
        {
            var testDir = TestContext.CurrentContext.TestDirectory;
            return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
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
