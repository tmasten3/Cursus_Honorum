using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Characters;
using NUnit.Framework;

public class CharacterFactoryStrictModeTests
{
    [Test]
    public void NormalMode_LoadsAndNormalizesInvalidEntries()
    {
        var path = WriteInvalidCharacterFile();
        try
        {
            var characters = CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Normal);
            Assert.That(characters, Is.Not.Null);
            Assert.That(characters.Count, Is.EqualTo(3));

            var maleWithBadNomen = characters[0];
            Assert.That(maleWithBadNomen.RomanName.Nomen, Is.EqualTo("Aurelius"));
            Assert.That(maleWithBadNomen.Family, Is.EqualTo("Aurelius"));

            var femaleWithIllegalCognomen = characters[1];
            Assert.That(femaleWithIllegalCognomen.RomanName.Cognomen, Is.Null.Or.Empty);

            var patricianTemplate = characters[2];
            Assert.That(patricianTemplate.RomanName.Praenomen, Is.Not.Null.And.Not.Empty);
            Assert.That(patricianTemplate.RomanName.Nomen, Is.Not.Null.And.Not.Empty);
            Assert.That(patricianTemplate.RomanName.Cognomen, Is.Not.Null.And.Not.Empty);
            Assert.That(patricianTemplate.Family, Is.EqualTo(patricianTemplate.RomanName.Nomen));

            Assert.IsTrue(CharacterFactory.LastValidationResult.Success);
            Assert.That(CharacterFactory.LastValidationResult.Issues, Is.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void StrictMode_ReturnsIssuesWithoutModifyingCharacters()
    {
        var path = WriteInvalidCharacterFile();
        try
        {
            var characters = CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Strict);
            var result = CharacterFactory.LastValidationResult;

            Assert.IsFalse(result.Success);
            Assert.That(result.Issues.Count, Is.EqualTo(7));

            Assert.That(characters[0].RomanName.Nomen, Is.EqualTo("aurelia"));
            Assert.That(characters[0].Family, Is.EqualTo(string.Empty));
            Assert.That(characters[1].RomanName.Cognomen, Is.EqualTo("Minor"));
            Assert.That(characters[2].RomanName.Praenomen, Is.EqualTo(string.Empty));
            Assert.That(characters[2].RomanName.Cognomen, Is.EqualTo(string.Empty));

            var fields = result.Issues.Select(i => i.Field).ToList();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "RomanName.Nomen", "Family", // character 0
                    "RomanName.Cognomen",          // character 1
                    "RomanName.Praenomen", "RomanName.Nomen", "RomanName.Cognomen", "Family" // character 2
                },
                fields);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void StrictModeFlagsSameFieldsAdjustedByNormalization()
    {
        var path = WriteInvalidCharacterFile();
        try
        {
            var strictCharacters = CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Strict);
            var strictIssues = CharacterFactory.LastValidationResult.Issues.ToList();

            var normalizedCharacters = CharacterFactory.LoadBaseCharacters(path, CharacterLoadMode.Normal);

            foreach (var issue in strictIssues)
            {
                var original = strictCharacters[issue.CharacterIndex];
                var normalized = normalizedCharacters[issue.CharacterIndex];

                switch (issue.Field)
                {
                    case "RomanName.Nomen":
                        Assert.That(original.RomanName?.Nomen, Is.Not.EqualTo(normalized.RomanName?.Nomen));
                        break;
                    case "RomanName.Praenomen":
                        Assert.That(original.RomanName?.Praenomen, Is.Not.EqualTo(normalized.RomanName?.Praenomen));
                        break;
                    case "RomanName.Cognomen":
                        Assert.That(original.RomanName?.Cognomen, Is.Not.EqualTo(normalized.RomanName?.Cognomen));
                        break;
                    case "Family":
                        Assert.That(original.Family, Is.Not.EqualTo(normalized.Family));
                        break;
                    default:
                        Assert.Fail($"Unexpected field '{issue.Field}' in validation issues.");
                        break;
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteInvalidCharacterFile()
    {
        var json = @"{
    \"Characters\": [
        {
            \"ID\": 100,
            \"Gender\": 0,
            \"BirthYear\": -100,
            \"BirthMonth\": 1,
            \"BirthDay\": 1,
            \"Age\": 30,
            \"Family\": \"\",
            \"Class\": 1,
            \"RomanName\": {
                \"Praenomen\": \"gaius\",
                \"Nomen\": \"aurelia\",
                \"Cognomen\": \"Felix\",
                \"Gender\": 0
            }
        },
        {
            \"ID\": 101,
            \"Gender\": 1,
            \"BirthYear\": -102,
            \"BirthMonth\": 1,
            \"BirthDay\": 1,
            \"Age\": 28,
            \"Family\": \"sempronia\",
            \"Class\": 1,
            \"RomanName\": {
                \"Praenomen\": \"\",
                \"Nomen\": \"sempronia\",
                \"Cognomen\": \"Minor\",
                \"Gender\": 1
            }
        },
        {
            \"ID\": 102,
            \"Gender\": 0,
            \"BirthYear\": -98,
            \"BirthMonth\": 1,
            \"BirthDay\": 1,
            \"Age\": 27,
            \"Family\": \"domitia\",
            \"Class\": 0,
            \"RomanName\": {
                \"Praenomen\": \"\",
                \"Nomen\": \"\",
                \"Cognomen\": \"\",
                \"Gender\": 0
            }
        }
    ]
}";

        var path = Path.Combine(Path.GetTempPath(), $"character_strict_mode_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
