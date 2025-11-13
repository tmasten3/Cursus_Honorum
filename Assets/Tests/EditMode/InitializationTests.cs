using NUnit.Framework;
using Game.Core;
using Game.Systems.BirthSystem;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.MarriageSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

public class InitializationTests
{
    [Test]
    public void GameState_Initializes_Without_Exception()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var gameState = new GameState(profile);
        Assert.DoesNotThrow(gameState.Initialize);
    }

    [Test]
    public void AllCoreSystems_ArePresentAfterInitialization()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var gameState = new GameState(profile);
        gameState.Initialize();

        Assert.IsNotNull(gameState.GetSystem<EventBus>());
        Assert.IsNotNull(gameState.GetSystem<TimeSystem>());
        Assert.IsNotNull(gameState.GetSystem<CharacterSystem>());
        Assert.IsNotNull(gameState.GetSystem<OfficeSystem>());
        Assert.IsNotNull(gameState.GetSystem<ElectionSystem>());
        Assert.IsNotNull(gameState.GetSystem<BirthSystem>());
        Assert.IsNotNull(gameState.GetSystem<MarriageSystem>());
    }
}
