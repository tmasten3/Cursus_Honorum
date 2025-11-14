using NUnit.Framework;
using Game.Core;
using Game.Systems.Time;

public class TimeSystemTests
{
    [Test]
    public void Date_Starts_Jan1_248BC()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var state = new GameState(profile);
        state.Initialize();

        var time = state.GetSystem<TimeSystem>();
        var date = time.CurrentDate;

        Assert.AreEqual(-248, date.Year);
        Assert.AreEqual(1, date.Month);
        Assert.AreEqual(1, date.Day);
    }

    [Test]
    public void AdvanceOneDay_IncrementsDayCorrectly()
    {
        var profile = SystemBootstrapProfile.CreateDefaultProfile();
        var state = new GameState(profile);
        state.Initialize();

        var time = state.GetSystem<TimeSystem>();
        time.StepDays(1);

        Assert.AreEqual(2, time.CurrentDate.Day);
    }
}
