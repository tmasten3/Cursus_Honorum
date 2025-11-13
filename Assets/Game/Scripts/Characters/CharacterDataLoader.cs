using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Characters;

namespace Game.Systems.Characters
{
    internal sealed class CharacterDataLoader
    {
        public List<Character> LoadBaseCharacters(string path)
        {
            try
            {
                var characters = CharacterFactory.LoadBaseCharacters(path) ?? new List<Character>();
                foreach (var character in characters)
                {
                    if (character == null)
                        continue;

                    if (character.RomanName == null)
                        Logger.Warn("Safety", $"{path}: Character #{character.ID} missing RomanName definition.");

                    if (string.IsNullOrWhiteSpace(character.Family))
                        Logger.Warn("Safety", $"{path}: Character #{character.ID} missing family information.");
                }

                return characters;
            }
            catch (Exception ex)
            {
                Logger.Error("Safety", $"Failed to load base character file '{path}': {ex.Message}");
                return new List<Character>();
            }
        }
    }
}
