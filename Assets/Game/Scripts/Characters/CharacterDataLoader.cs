using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.Characters.Generation;

namespace Game.Systems.Characters
{
    internal sealed class CharacterDataLoader
    {
        public List<Character> LoadBaseCharacters(SimulationConfig.CharacterSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                RomanFamilyRegistry.Reset();
                var generator = new BaseCharactersGenerator(settings.BasePopulationSeed, -248);
                var characters = generator.Generate();
                foreach (var character in characters)
                {
                    if (character == null)
                        continue;

                    if (character.RomanName == null)
                        Logger.Warn("Safety", $"Generated character #{character.ID} missing RomanName definition.");

                    if (string.IsNullOrWhiteSpace(character.Family))
                        Logger.Warn("Safety", $"Generated character #{character.ID} missing family information.");
                }

                return characters;
            }
            catch (Exception ex)
            {
                Logger.Error("Safety", $"Failed to generate base population: {ex.Message}");
                return new List<Character>();
            }
        }
    }
}
