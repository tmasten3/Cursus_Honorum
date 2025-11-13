using NUnit.Framework;
using Game.Core;
using Game.Systems.CharacterSystem;

public class CharacterSystemTests
{
    [Test]
    public void BaseCharacters_Load_Without_Exception()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var state = new GameState(profile);
        Assert.DoesNotThrow(state.Initialize);
    }

    [Test]
    public void BaseCharacters_AreLoaded_AndPresent()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var state = new GameState(profile);
        state.Initialize();

        var charSys = state.GetSystem<CharacterSystem>();
        Assert.Greater(charSys.GetLiveCharacterCount(), 0);
    }
}
