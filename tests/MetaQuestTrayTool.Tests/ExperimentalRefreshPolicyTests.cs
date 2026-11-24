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

    [Fact]
    public void PropertyReadbackIsNotTreatedAsTheEffectivePanelRate()
    {
        var missing = ExperimentalRefreshPolicy.Interpret(207, "207", null);
        Assert.True(missing.PropertyMatched);
        Assert.Equal(PanelEvidence.NotRead, missing.Panel);
        Assert.Contains("not measured", missing.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fresh delivered FPS", missing.Summary, StringComparison.Ordinal);

        var different = ExperimentalRefreshPolicy.Interpret(207, "207", "mCurrentRefreshRate=120.0");
        Assert.Equal(PanelEvidence.Differs, different.Panel);
        Assert.Equal(120, different.EffectivePanelHz);

        var matched = ExperimentalRefreshPolicy.Interpret(144, "144", "refreshRate=144.0");
        Assert.Equal(PanelEvidence.Matches, matched.Panel);
        Assert.Contains("not a streaming frame rate", matched.Summary, StringComparison.OrdinalIgnoreCase);

        var mixed = ExperimentalRefreshPolicy.Interpret(144, "144", "mCurrentRefreshRate=144\nrefreshRate=72");
        Assert.Equal(PanelEvidence.Unparsed, mixed.Panel);
        Assert.Null(mixed.EffectivePanelHz);
    }
}
