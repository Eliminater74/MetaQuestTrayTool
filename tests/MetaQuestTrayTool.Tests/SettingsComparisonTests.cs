using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class SettingsComparisonTests
{
    [Fact]
    public void AbbaAppliesEachLegAndRestoresAfterSuccess()
    {
        var clock = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var applied = new List<string>();
        var restored = 0;
        var outcome = SettingsComparison.Run(
            new ComparisonRequest
            {
                Setting = "telemetry",
                Order = ComparisonOrder.Abba,
                DurationSeconds = 30,
                WarmupSeconds = 0,
                Baseline = "72",
                Candidate = "90"
            },
            applied.Add,
            () => applied.Count,
            () => 40,
            () => clock,
            delay => clock += delay,
            () => restored++,
            CancellationToken.None);

        Assert.True(outcome.Completed);
        Assert.True(outcome.Restored);
        Assert.Equal(1, restored);
        Assert.Equal(new[] { "72", "90", "72" }, applied);
        Assert.Equal(3, outcome.Legs.Count);
        Assert.Contains("not calculated", outcome.StatisticsNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("within 5 C", outcome.ThermalNote, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamVR was restarted", outcome.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void UnauthorizedAndSteamVrComparisonsChangeNothing()
    {
        var applied = 0;
        var restored = 0;
        var reconnect = SettingsComparison.Run(Request(BenchmarkDisruption.RequiresReconnect, false), _ => applied++, () => 1, null, () => DateTimeOffset.UtcNow, _ => { }, () => restored++, CancellationToken.None);
        var restart = SettingsComparison.Run(Request(BenchmarkDisruption.RequiresSteamVrRestart, true), _ => applied++, () => 1, null, () => DateTimeOffset.UtcNow, _ => { }, () => restored++, CancellationToken.None);
        Assert.True(reconnect.Refused);
        Assert.True(restart.Refused);
        Assert.Equal(0, applied);
        Assert.Equal(0, restored);
    }

    [Fact]
    public void CancellationAndApplyFailureStillRestore()
    {
        var clock = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var restored = 0;
        var cancelled = SettingsComparison.Run(
            Request(BenchmarkDisruption.NonDisruptive, false),
            _ => { },
            () => 1,
            () => null,
            () => clock,
            delay => clock += delay,
            () => restored++,
            cancel.Token);
        Assert.True(cancelled.Cancelled);
        Assert.True(cancelled.Restored);
        Assert.Equal(1, restored);
        Assert.Contains("not established", cancelled.ThermalNote, StringComparison.OrdinalIgnoreCase);

        var failed = SettingsComparison.Run(
            Request(BenchmarkDisruption.NonDisruptive, false),
            _ => throw new IOException("apply failed"),
            () => 1,
            () => 36,
            () => clock,
            delay => clock += delay,
            () => throw new IOException("restore failed"),
            CancellationToken.None);
        Assert.False(failed.Completed);
        Assert.True(failed.RestoreFailed);
        Assert.Contains("restore failed", failed.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThermalSpreadIsReportedAndHistoryKeepsTheLatestRuns()
    {
        var clock = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var temperatures = new Queue<double?>(new double?[] { 30, 40 });
        var outcome = SettingsComparison.Run(
            new ComparisonRequest { Order = ComparisonOrder.Ab, DurationSeconds = 30, Setting = "telemetry" },
            _ => { },
            () => 10,
            () => temperatures.Dequeue(),
            () => clock,
            delay => clock += delay,
            () => { },
            CancellationToken.None);
        Assert.Contains("differed", outcome.ThermalNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a hardware safety limit", outcome.ThermalNote, StringComparison.OrdinalIgnoreCase);

        var path = Path.Combine(Path.GetTempPath(), "mqt-compare-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            for (var i = 0; i < 22; i++)
            {
                ComparisonHistory.Append(path, outcome);
            }

            var saved = JsonSerializerCount(path);
            Assert.Equal(ComparisonHistory.MaxStored, saved);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static ComparisonRequest Request(BenchmarkDisruption disruption, bool authorized) => new()
    {
        Disruption = disruption,
        Authorized = authorized,
        DurationSeconds = 30,
        Order = ComparisonOrder.Ab
    };

    private static int JsonSerializerCount(string path)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetArrayLength();
    }
}
