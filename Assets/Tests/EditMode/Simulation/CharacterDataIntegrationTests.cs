using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CursusHonorum.Tests.Simulation
{
    public class CharacterDataIntegrationTests
    {
        private readonly List<string> tempFiles = new();

        [TearDown]
        public void Cleanup()
        {
            foreach (var file in tempFiles)
            {
                try
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                        File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup failures in tests.
                }
            }
            tempFiles.Clear();
        }

        [Test]
        public void LoadBaseCharacters_PreservesPoliticalAndOfficeData()
        {
            var wrapper = new CharacterDataWrapper();
            wrapper.Characters.Add(new Character
            {
                ID = 42,
                RomanName = new RomanName("Lucius", "Cornelius", "Sulla", Gender.Male),
                Gender = Gender.Male,
                BirthYear = -138,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 30,
                Family = "Cornelius",
                Class = SocialClass.Patrician,
                SenatorialInfluence = 12.5f,
                PopularInfluence = 9.5f,
                MilitaryInfluence = 3.1f,
                FamilyInfluence = 4.2f,
                Oratory = 17,
                AmbitionScore = 12,
                Courage = 15,
                Dignitas = 18,
                Administration = 11,
                Judgment = 10,
                Strategy = 16,
                Civic = 7,
                Faction = FactionType.Optimates,
                CurrentOffice = new OfficeAssignment
                {
                    OfficeId = "Consul",
                    SeatIndex = 1,
                    StartYear = -88
                },
                OfficeHistory = new List<OfficeHistoryEntry>
                {
                    new OfficeHistoryEntry
                    {
                        OfficeId = "Quaestor",
                        SeatIndex = 0,
                        StartYear = -92,
                        EndYear = -91,
                        Notes = "Served in Asia"
                    }
                }
            });

            string path = WriteTempJson(wrapper);

            var result = CharacterFactory.LoadBaseCharacters(path);
            Assert.That(result, Has.Count.EqualTo(1));
            var character = result.Single();

            Assert.That(character.SenatorialInfluence, Is.EqualTo(12.5f));
            Assert.That(character.PopularInfluence, Is.EqualTo(9.5f));
            Assert.That(character.MilitaryInfluence, Is.EqualTo(3.1f));
            Assert.That(character.FamilyInfluence, Is.EqualTo(4.2f));
            Assert.That(character.Oratory, Is.EqualTo(17));
            Assert.That(character.AmbitionScore, Is.EqualTo(12));
            Assert.That(character.Courage, Is.EqualTo(15));
            Assert.That(character.Dignitas, Is.EqualTo(18));
            Assert.That(character.Administration, Is.EqualTo(11));
            Assert.That(character.Judgment, Is.EqualTo(10));
            Assert.That(character.Strategy, Is.EqualTo(16));
            Assert.That(character.Civic, Is.EqualTo(7));
            Assert.That(character.Faction, Is.EqualTo(FactionType.Optimates));

            Assert.That(character.CurrentOffice.OfficeId, Is.EqualTo("Consul"));
            Assert.That(character.CurrentOffice.SeatIndex, Is.EqualTo(1));
            Assert.That(character.CurrentOffice.StartYear, Is.EqualTo(-88));

            Assert.That(character.OfficeHistory, Is.Not.Null);
            Assert.That(character.OfficeHistory, Has.Count.EqualTo(1));
            Assert.That(character.OfficeHistory[0].OfficeId, Is.EqualTo("Quaestor"));
            Assert.That(character.OfficeHistory[0].SeatIndex, Is.EqualTo(0));
            Assert.That(character.OfficeHistory[0].StartYear, Is.EqualTo(-92));
            Assert.That(character.OfficeHistory[0].EndYear, Is.EqualTo(-91));
        }

        [Test]
        public void LoadBaseCharacters_DefaultsMissingPoliticalFields()
        {
            string json = "{" +
                          "\"Characters\":[{" +
                          "\"ID\":1," +
                          "\"RomanName\":{\"Praenomen\":\"Gaius\",\"Nomen\":\"Julius\",\"Cognomen\":\"Caesar\",\"Gender\":0}," +
                          "\"Gender\":0," +
                          "\"BirthYear\":-100," +
                          "\"BirthMonth\":1," +
                          "\"BirthDay\":1," +
                          "\"Age\":35," +
                          "\"Family\":\"Julius\"," +
                          "\"Class\":0" +
                          "}]}";

            string path = WriteTempJson(json);

            var result = CharacterFactory.LoadBaseCharacters(path);
            Assert.That(result, Has.Count.EqualTo(1));
            var character = result[0];

            Assert.That(character.SenatorialInfluence, Is.EqualTo(0f));
            Assert.That(character.PopularInfluence, Is.EqualTo(0f));
            Assert.That(character.MilitaryInfluence, Is.EqualTo(0f));
            Assert.That(character.FamilyInfluence, Is.EqualTo(0f));
            Assert.That(character.Oratory, Is.EqualTo(5));
            Assert.That(character.AmbitionScore, Is.EqualTo(5));
            Assert.That(character.Courage, Is.EqualTo(5));
            Assert.That(character.Dignitas, Is.EqualTo(5));
            Assert.That(character.Administration, Is.EqualTo(5));
            Assert.That(character.Judgment, Is.EqualTo(5));
            Assert.That(character.Strategy, Is.EqualTo(5));
            Assert.That(character.Civic, Is.EqualTo(5));
            Assert.That(character.Faction, Is.EqualTo(FactionType.Neutral));
            Assert.That(character.OfficeHistory, Is.Not.Null);
            Assert.That(character.OfficeHistory, Is.Empty);
            Assert.That(character.CurrentOffice.OfficeId, Is.Null.Or.Empty);
        }

        [Test]
        public void ValidatorFlagsInvalidPoliticalData()
        {
            var character = new Character
            {
                ID = 5,
                RomanName = new RomanName("Marcus", "Tullius", "Cicero", Gender.Male),
                Gender = Gender.Male,
                BirthYear = -106,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 40,
                Family = "Tullius",
                Class = SocialClass.Patrician,
                SenatorialInfluence = float.NaN,
                PopularInfluence = -5f,
                MilitaryInfluence = float.PositiveInfinity,
                FamilyInfluence = 2f,
                Oratory = 25,
                AmbitionScore = -3,
                Courage = 21,
                Dignitas = 0,
                Administration = -1,
                Judgment = 30,
                Strategy = 5,
                Civic = 40,
                Faction = (FactionType)999,
                CurrentOffice = new OfficeAssignment
                {
                    OfficeId = "Consul",
                    SeatIndex = -2,
                    StartYear = -63
                },
                OfficeHistory = new List<OfficeHistoryEntry>
                {
                    null,
                    new OfficeHistoryEntry
                    {
                        OfficeId = "Praetor",
                        SeatIndex = -1,
                        StartYear = -66,
                        EndYear = -70
                    }
                }
            };

            var issues = new List<CharacterValidationIssue>();
            CharacterDataValidator.Validate(new[] { character }, "Test", issues.Add);

            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.SenatorialInfluence)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.PopularInfluence)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.MilitaryInfluence)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Oratory)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.AmbitionScore)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Courage)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Administration)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Judgment)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Civic)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(Character.Faction)));
            Assert.That(issues.Select(i => i.Field), Does.Contain(nameof(OfficeAssignment.SeatIndex)));
            Assert.That(issues.Select(i => i.Field), Does.Contain("OfficeHistory[1].SeatIndex"));
            Assert.That(issues.Select(i => i.Field), Does.Contain("OfficeHistory[1].EndYear"));
        }

        [Test]
        public void NormalizationSanitizesInvalidPoliticalData()
        {
            var character = new Character
            {
                ID = 7,
                RomanName = new RomanName("Gnaeus", "Pompeius", "Magnus", Gender.Male),
                Gender = Gender.Male,
                BirthYear = -106,
                BirthMonth = 9,
                BirthDay = 29,
                Age = 35,
                Family = "Pompeius",
                Class = SocialClass.Patrician,
                SenatorialInfluence = -10f,
                PopularInfluence = float.NaN,
                MilitaryInfluence = float.PositiveInfinity,
                FamilyInfluence = 3f,
                Oratory = 30,
                AmbitionScore = -10,
                Courage = 50,
                Dignitas = 5,
                Administration = 25,
                Judgment = -5,
                Strategy = 18,
                Civic = 99,
                Faction = (FactionType)(-1),
                CurrentOffice = new OfficeAssignment
                {
                    OfficeId = "  Praetor  ",
                    SeatIndex = -4,
                    StartYear = -100
                },
                OfficeHistory = new List<OfficeHistoryEntry>
                {
                    null,
                    new OfficeHistoryEntry
                    {
                        OfficeId = "  Quaestor  ",
                        SeatIndex = -2,
                        StartYear = -95,
                        EndYear = -99
                    }
                }
            };

            CharacterFactory.NormalizeDeserializedCharacter(character, "TestSource");

            Assert.That(character.SenatorialInfluence, Is.EqualTo(0f));
            Assert.That(character.PopularInfluence, Is.EqualTo(0f));
            Assert.That(character.MilitaryInfluence, Is.EqualTo(0f));
            Assert.That(character.FamilyInfluence, Is.EqualTo(3f));

            Assert.That(character.Oratory, Is.EqualTo(20));
            Assert.That(character.AmbitionScore, Is.EqualTo(0));
            Assert.That(character.Courage, Is.EqualTo(20));
            Assert.That(character.Dignitas, Is.EqualTo(5));
            Assert.That(character.Administration, Is.EqualTo(20));
            Assert.That(character.Judgment, Is.EqualTo(0));
            Assert.That(character.Strategy, Is.EqualTo(18));
            Assert.That(character.Civic, Is.EqualTo(20));

            Assert.That(character.Faction, Is.EqualTo(FactionType.Neutral));
            Assert.That(character.CurrentOffice.OfficeId, Is.EqualTo("Praetor"));
            Assert.That(character.CurrentOffice.SeatIndex, Is.EqualTo(0));
            Assert.That(character.CurrentOffice.StartYear, Is.EqualTo(0));

            Assert.That(character.OfficeHistory, Has.Count.EqualTo(1));
            var history = character.OfficeHistory[0];
            Assert.That(history.OfficeId, Is.EqualTo("Quaestor"));
            Assert.That(history.SeatIndex, Is.EqualTo(0));
            Assert.That(history.StartYear, Is.EqualTo(0));
            Assert.That(history.EndYear, Is.EqualTo(0));
        }

        private string WriteTempJson(CharacterDataWrapper wrapper)
        {
            string path = Path.Combine(Path.GetTempPath(), $"characters_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonUtility.ToJson(wrapper));
            tempFiles.Add(path);
            return path;
        }

        private string WriteTempJson(string json)
        {
            string path = Path.Combine(Path.GetTempPath(), $"characters_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            tempFiles.Add(path);
            return path;
        }
    }
}
