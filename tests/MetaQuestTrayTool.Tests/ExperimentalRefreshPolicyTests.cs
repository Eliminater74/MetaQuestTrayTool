using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class ExperimentalRefreshPolicyTests
{
    [Theory]
    [InlineData(144, true)]
    [InlineData(207, true)]
    [InlineData(240, false)]
    [InlineData(165, false)]
    [InlineData(180, false)]
    [InlineData(200, false)]
    public void Quest3ExposesOnly144And207AsPropertyWrites(int hz, bool canApply)
    {
        var mode = ExperimentalRefreshPolicy.ModesFor(HeadsetFamily.Quest3).Single(item => item.Hz == hz);
        Assert.Equal(canApply, mode.CanApply);
        Assert.DoesNotContain(ExperimentalRefreshPolicy.ModesFor(HeadsetFamily.Quest3), item => item.Reason.Contains(ExperimentalRefreshPolicy.ScalingProperty, StringComparison.Ordinal) && item.CanApply);
    }

    [Fact]
    public void Quest3SAndDisabledFlagDoNotApply()
    {
        Assert.False(ExperimentalRefreshPolicy.Evaluate(true, "Quest 3S", 207, false).Apply);
        Assert.False(ExperimentalRefreshPolicy.Evaluate(false, "Quest 3", 207, false).Apply);
        var conflict = ExperimentalRefreshPolicy.Evaluate(true, "Quest 3", 144, standardRefreshAlsoSet: true);
        Assert.False(conflict.Apply);
        Assert.Contains("device default", conflict.Refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowedQuest3RequestWarnsThatLinkFpsIsSeparate()
    {
        var decision = ExperimentalRefreshPolicy.Evaluate(true, "Quest 3", 207, false);
        Assert.True(decision.Apply);
        Assert.Equal(207, decision.Hz);
        Assert.Contains("streaming", decision.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ExperimentalRefreshPolicy.ScalingProperty, decision.Warning, StringComparison.Ordinal);
    }
}
