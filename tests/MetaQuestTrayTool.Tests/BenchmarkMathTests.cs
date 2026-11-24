using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class BenchmarkMathTests
{
    [Fact]
    public void PeriodicFpsDoesNotInventOnePercentLows()
    {
        var report = BenchmarkMath.FromPeriodicFps([90, 100, 110, 80]);
        Assert.False(report.FrameLevel);
        Assert.Null(report.OnePercentLow);
        Assert.Null(report.PointOnePercentLow);
        Assert.Contains("too coarse", report.PercentileNote, StringComparison.Ordinal);
        Assert.Equal(80, report.Minimum);
        Assert.Equal(110, report.Maximum);
        Assert.Contains("unavailable", report.ToDisplayText(), StringComparison.Ordinal);
    }

    [Fact]
    public void FrameTimesProduceLowsAndShortSeriesDoNot()
    {
        var times = Enumerable.Repeat(8.33, 90).Append(20).ToList();
        var shortReport = BenchmarkMath.FromFrameTimesMilliseconds(times);
        Assert.Null(shortReport.OnePercentLow);
        Assert.Contains("at least 100", shortReport.PercentileNote, StringComparison.Ordinal);

        var longer = Enumerable.Repeat(8.33, 990).Concat(Enumerable.Repeat(20d, 10)).ToList();
        var report = BenchmarkMath.FromFrameTimesMilliseconds(longer);
        Assert.True(report.FrameLevel);
        Assert.NotNull(report.OnePercentLow);
        Assert.InRange(report.OnePercentLow!.Value, 40, 60);
        Assert.NotNull(report.PointOnePercentLow);
        Assert.InRange(report.PointOnePercentLow!.Value, 40, 60);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(30, 30)]
    [InlineData(900, 300)]
    public void CustomDurationStaysInsideFiveSecondsToFiveMinutes(int requested, int expected) =>
        Assert.Equal(expected, BenchmarkMath.ClampCustomDuration(requested));

    [Fact]
    public void UnauthorizedInterruptionDoesNotApplyOrForgetRestore()
    {
        var calls = new List<string>();
        var text = BenchmarkMath.Compare(
            interruptsSession: true,
            authorized: false,
            () => calls.Add("A"),
            () => calls.Add("B"),
            () => "measured",
            () => calls.Add("restore"));
        Assert.Contains("not authorized", text, StringComparison.Ordinal);
        Assert.Empty(calls);
    }

    [Fact]
    public void ComparisonRestoresWhenMeasurementThrows()
    {
        var calls = new List<string>();
        Assert.Throws<InvalidOperationException>(() => BenchmarkMath.Compare(
            false,
            false,
            () => calls.Add("A"),
            () => calls.Add("B"),
            () => throw new InvalidOperationException("sample failed"),
            () => calls.Add("restore")));
        Assert.Equal(["A", "restore"], calls);
    }
}
