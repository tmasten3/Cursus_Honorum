using System.IO;
using Game.Core;
using NUnit.Framework;
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

            var characterSystem = new CharacterSystem(eventBus, timeSystem);
            characterSystem.Initialize(null);

            var marriageSystem = new MarriageSystem(eventBus, characterSystem);
            marriageSystem.Initialize(null);

            var birthSystem = new BirthSystem(eventBus, characterSystem);
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
