using System;
using System.Collections.Generic;
using Game.Data.Characters;

namespace Game.Systems.CharacterSystem
{
    internal sealed class CharacterMortalityService
    {
        private readonly CharacterRepository repository;
        private readonly System.Random rng;
        private readonly Func<int, float> hazardProvider;

        public CharacterMortalityService(CharacterRepository repository, System.Random rng, Func<int, float> hazardProvider)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            this.hazardProvider = hazardProvider ?? throw new ArgumentNullException(nameof(hazardProvider));
        }

        public IReadOnlyList<int> SelectDailyDeaths()
        {
            var toKill = new List<int>();

            foreach (var id in repository.EnumerateLivingIds())
            {
                var character = repository.Get(id);
                if (character == null) continue;

                float hazard = hazardProvider(character.Age);
                if (hazard <= 0f) continue;

                if (rng.NextDouble() < hazard)
                    toKill.Add(id);
            }

            return toKill;
        }
    }
}
