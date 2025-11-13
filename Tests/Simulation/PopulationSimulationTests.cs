using System.IO;
using NUnit.Framework;
using Game.Core;
using Game.Systems.EventBus;
using Game.Systems.Time;
using Game.Systems.CharacterSystem;
using Game.Systems.MarriageSystem;
using Game.Systems.BirthSystem;
using UnityEngine;

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

            var eventBus = new EventBus();
            eventBus.Initialize(null);

            var timeSystem = new TimeSystem(eventBus);
            timeSystem.Initialize(null);

            var simulationConfig = SimulationConfigLoader.LoadOrDefault();

            var characterSystem = new CharacterSystem(eventBus, timeSystem, simulationConfig);
            characterSystem.Initialize(null);

            var marriageSystem = new MarriageSystem(eventBus, characterSystem, simulationConfig);
            marriageSystem.Initialize(null);

            var birthSystem = new BirthSystem(eventBus, characterSystem, simulationConfig);
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
            private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

            public SimulationDate NextDay(out bool rolledMonth, out bool rolledYear)
            {
                int year = Year;
                int month = Month;
                int day = Day + 1;

                rolledMonth = false;
                rolledYear = false;

                if (day > DaysInMonth[month - 1])
                {
                    day = 1;
                    month++;
                    rolledMonth = true;

                    if (month > 12)
                    {
                        month = 1;
                        year++;
                        if (year == 0)
                            year = 1;
                        rolledYear = true;
                    }
                }

                return new SimulationDate(year, month, day);
            }
        }
    }
}
