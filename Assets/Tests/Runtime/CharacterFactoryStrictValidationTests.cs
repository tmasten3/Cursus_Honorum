using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Data.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CursusHonorum.Tests.Runtime
{
    public class CharacterFactoryStrictValidationTests
    {
        [Test]
        public void StrictValidationReportsRelationshipIssues()
        {
            var wrapper = new CharacterDataWrapper();
            wrapper.Characters.Add(new Character
            {
                ID = 1,
                RomanName = new RomanName("Gaius", "Julius", "Caesar", Gender.Male),
                Gender = Gender.Male,
                BirthYear = -100,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 35,
                Family = "Julius",
                Class = SocialClass.Patrician,
                FatherID = 1,
                MotherID = null,
                SpouseID = null
            });

            wrapper.Characters.Add(new Character
            {
                ID = 2,
                RomanName = new RomanName(null, "Julia", null, Gender.Female),
                Gender = Gender.Female,
                BirthYear = -98,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 33,
                Family = "Julius",
                Class = SocialClass.Patrician,
                SpouseID = 2,
                FatherID = 1,
                MotherID = null
            });

            var path = WriteCharacters(wrapper);

            try
            {
                CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Strict);
                var result = CharacterFactory.LastValidationResult;

                Assert.IsFalse(result.Success, "Strict validation should report failures for self-referential relationships.");
                Assert.That(result.Issues.Any(i => i.Field == "Relationships.Parent" && i.Message.Contains("their own parent")),
                    "Expected a parent relationship issue to be reported.");
                Assert.That(result.Issues.Any(i => i.Field == "Relationships.Spouse" && i.Message.Contains("married to themselves")),
                    "Expected a spouse relationship issue to be reported.");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void StrictValidationDoesNotAdvanceRomanNamingGlobalRng()
        {
            var wrapper = new CharacterDataWrapper();
            wrapper.Characters.Add(new Character
            {
                ID = 3,
                RomanName = null,
                Gender = Gender.Male,
                BirthYear = -120,
                BirthMonth = 1,
                BirthDay = 1,
                Age = 40,
                Family = "Cornelius",
                Class = SocialClass.Patrician
            });

            var path = WriteCharacters(wrapper);

            var rngField = typeof(RomanNamingRules)
                .GetField("rng", BindingFlags.Static | BindingFlags.NonPublic);
            var originalRandom = (System.Random)rngField!.GetValue(null);
            var trackingRandom = new TrackingRandom(12345);
            rngField.SetValue(null, trackingRandom);

            try
            {
                CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Strict);
                Assert.That(trackingRandom.NextCallCount, Is.EqualTo(0),
                    "Strict validation should not consume the global Roman naming RNG.");
            }
            finally
            {
                rngField.SetValue(null, originalRandom);
                File.Delete(path);
            }
        }

        private static string WriteCharacters(CharacterDataWrapper wrapper)
        {
            var path = Path.Combine(Path.GetTempPath(), $"base_characters_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonUtility.ToJson(wrapper));
            return path;
        }

        private sealed class TrackingRandom : System.Random
        {
            public int NextCallCount { get; private set; }

            public TrackingRandom(int seed) : base(seed)
            {
            }

            public override int Next()
            {
                NextCallCount++;
                return base.Next();
            }

            public override int Next(int maxValue)
            {
                NextCallCount++;
                return base.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                NextCallCount++;
                return base.Next(minValue, maxValue);
            }

            public override void NextBytes(byte[] buffer)
            {
                NextCallCount++;
                base.NextBytes(buffer);
            }

            public override double NextDouble()
            {
                NextCallCount++;
                return base.NextDouble();
            }
        }
    }
}
