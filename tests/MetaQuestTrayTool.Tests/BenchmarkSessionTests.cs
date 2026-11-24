using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class BenchmarkSessionTests
{
    [Fact]
    public void TimedSessionDiscardsWarmupAndDoesNotInventFrameLows()
    {
        var clock = DateTimeOffset.Parse("2026-11-24T15:00:00Z");
        var calls = 0;
        var result = BenchmarkSession.Run(
            30,
            5,
            () => clock,
            () => calls < 5 ? 10 : 90,
            delay =>
            {
                calls++;
                clock += delay;
            },
            CancellationToken.None,
            "PC GPU %");

        Assert.False(result.Cancelled);
        Assert.Equal(BenchmarkDisruption.NonDisruptive, result.Disruption);
        Assert.DoesNotContain("1% low: 10", result.Report.ToDisplayText(), StringComparison.Ordinal);
        Assert.Null(result.Report.OnePercentLow);
        Assert.True(result.Report.SampleCount > 0);
        Assert.True(result.Report.Minimum >= 90);
        Assert.Contains("nothing to restore", result.RestoreNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CancelStopsBeforeTheDeadline()
    {
        var clock = DateTimeOffset.Parse("2026-11-24T15:00:00Z");
        using var cancel = new CancellationTokenSource();
        var result = BenchmarkSession.Run(
            300,
            5,
            () => clock,
            () => 50,
            delay =>
            {
                clock += delay;
                cancel.Cancel();
            },
            cancel.Token);

        Assert.True(result.Cancelled);
        Assert.True(result.Report.SampleCount < 300);
    }

    [Theory]
    [InlineData("telemetry", BenchmarkDisruption.NonDisruptive)]
    [InlineData("bitrate", BenchmarkDisruption.RequiresReconnect)]
    [InlineData("steamvr-runtime", BenchmarkDisruption.RequiresSteamVrRestart)]
    [InlineData("unknown", BenchmarkDisruption.Unsupported)]
    public void ComparisonsAreClassifiedBeforeAnythingRuns(string name, BenchmarkDisruption expected) =>
        Assert.Equal(expected, BenchmarkSession.Classify(name));

    [Fact]
    public void UnauthorizedRestartIsNotInvoked()
    {
        var ran = false;
        var text = BenchmarkSession.GuardComparison(BenchmarkDisruption.RequiresSteamVrRestart, authorized: false, () => ran = true);
        Assert.False(ran);
        Assert.Contains("not authorized", text, StringComparison.OrdinalIgnoreCase);

        var allowed = BenchmarkSession.GuardComparison(BenchmarkDisruption.NonDisruptive, authorized: false, () => ran = true);
        Assert.True(ran);
        Assert.Contains("not restarted", allowed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoryKeepsABoundedExportableRecord()
    {
        var path = Path.Combine(Path.GetTempPath(), "mqt-bench-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var result = new BenchmarkSessionResult
            {
                DurationSeconds = 30,
                WarmupSeconds = 5,
                Metric = "FPS",
                Report = BenchmarkMath.FromPeriodicFps([90, 90])
            };
            BenchmarkHistoryStore.Append(path, result);
            Assert.Single(BenchmarkHistoryStore.Read(path));
            var csv = BenchmarkHistoryStore.ToCsv(result);
            Assert.Contains("unavailable", csv, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("30", csv, StringComparison.Ordinal);
            Assert.Contains("unavailable", result.ToDisplayText(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void RejectsDurationsOutsideThePresets() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BenchmarkSession.Run(
            15,
            0,
            () => DateTimeOffset.UnixEpoch,
            () => 1,
            _ => { },
            CancellationToken.None));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
        catch (IOException)
        {
        }
    }
}
