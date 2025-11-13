using NUnit.Framework;
using Game.Core;
using Game.Systems.Politics.Offices;

public class OfficeSystemTests
{
    [Test]
    public void OfficeDefinitions_Load_Without_Exception()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var state = new GameState(profile);
        state.Initialize();

        var officeSys = state.GetSystem<OfficeSystem>();
        Assert.Greater(officeSys.TotalOfficesCount, 0);
    }
}
