using System.Linq;
using NUnit.Framework;
using Game.Core;
using Game.Systems.CharacterSystem;

namespace CursusHonorum.Tests.Runtime
{
    public class CharacterSystemTests
    {
        private static GameState CreateInitializedState(out CharacterSystem characterSystem)
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            characterSystem = state.GetSystem<CharacterSystem>();
            Assert.IsNotNull(characterSystem, "CharacterSystem should be available from GameState.");
            return state;
        }

        [Test]
        public void BaseCharacters_LoadIntoRepository()
        {
            var state = CreateInitializedState(out var characterSystem);
            try
            {
                var living = characterSystem.GetAllLiving();
                Assert.IsNotNull(living);
                Assert.IsNotEmpty(living, "Base character dataset should seed living characters.");
                Assert.IsTrue(living.All(c => c != null && c.IsAlive));
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void LiveCharacterCount_IsPositive()
        {
            var state = CreateInitializedState(out var characterSystem);
            try
            {
                Assert.Greater(characterSystem.GetLiveCharacterCount(), 0);
                Assert.Greater(characterSystem.CountAlive(), 0);
            }
            finally
            {
                state.Shutdown();
            }
        }

        [Test]
        public void ValidateBaseCharacters_Passes()
        {
            var state = CreateInitializedState(out var characterSystem);
            try
            {
                var result = characterSystem.ValidateBaseCharactersOnly();
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Success, "Base character data should validate successfully.");
            }
            finally
            {
                state.Shutdown();
            }
        }
    }
}
