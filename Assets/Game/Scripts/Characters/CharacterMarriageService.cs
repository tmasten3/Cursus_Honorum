using System;
using Game.Systems.CharacterSystem;

namespace Game.Systems.Characters
{
    internal sealed class CharacterMarriageService
    {
        private readonly CharacterRepository repository;

        public CharacterMarriageService(CharacterRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public bool TryMarry(int firstId, int secondId)
        {
            var spouseA = repository.Get(firstId);
            var spouseB = repository.Get(secondId);

            if (spouseA == null || spouseB == null)
                return false;

            if (!spouseA.IsAlive || !spouseB.IsAlive)
                return false;

            if (spouseA.SpouseID.HasValue || spouseB.SpouseID.HasValue)
                return false;

            spouseA.SpouseID = spouseB.ID;
            spouseB.SpouseID = spouseA.ID;

            return true;
        }
    }
}
