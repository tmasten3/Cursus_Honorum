using System;
using System.Collections.Generic;
using Game.Data.Characters;
using Game.Systems.CharacterSystem;

namespace Game.Systems.Characters
{
    internal sealed class CharacterFamilyService
    {
        private readonly CharacterRepository repository;

        public CharacterFamilyService(CharacterRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void AddCharacter(Character character, bool keepDead)
        {
            repository.Add(character, keepDead);
        }

        public Character Get(int id) => repository.Get(id);

        public IReadOnlyList<Character> GetAllLiving() => repository.GetAllLiving();

        public IReadOnlyList<Character> GetByFamily(string gens) => repository.GetByFamily(gens);

        public IReadOnlyList<Character> GetByCognomen(string cognomen) => repository.GetByCognomen(cognomen);

        public IReadOnlyList<Character> GetByBranch(string branchId) => repository.GetByBranch(branchId);

        public IReadOnlyList<Character> GetByClass(SocialClass socialClass) => repository.GetByClass(socialClass);

        public int CountAlive() => repository.AliveCount;

        public int GetFamilyCount() => repository.FamilyCount;
    }
}
