using System;
using NUnit.Framework;
using Game.Core;

namespace CursusHonorum.Tests.Runtime
{
    internal static class TestGameStateHelper
    {
        public static GameState CreateInitializedState()
        {
            var profile = SystemBootstrapProfile.CreateDefaultProfile();
            var state = new GameState(profile);
            state.Initialize();
            return state;
        }

        public static T RequireSystem<T>(GameState state) where T : GameSystemBase
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var system = state.GetSystem<T>();
            Assert.IsNotNull(system, $"{typeof(T).Name} should be available from GameState.");
            return system;
        }
    }
}
