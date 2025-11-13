using System;
using Game.Core;
using Game.Systems.CharacterSystem;

namespace Game.Systems.Characters
{
    internal sealed class CharacterAgeService
    {
        private readonly CharacterRepository repository;

        public CharacterAgeService(CharacterRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void ProcessDailyAging(int month, int day)
        {
            try
            {
                repository.AgeUpBirthdays(month, day);
            }
            catch (Exception ex)
            {
                Logger.Warn("Safety", $"Birthday aging failed for {month}/{day}: {ex.Message}");
            }
        }
    }
}
