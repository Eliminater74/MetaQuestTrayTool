using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class LivePerformanceMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PauseAndStopDoNotSampleAndHistoryIsBounded()
    {
        var monitor = new LivePerformanceMonitor();
        var calls = 0;
        Assert.False(monitor.TryRecord([Fps(90)], EvidenceKind.LiveSample, Now));
        monitor.Start();
        monitor.Pause();
        Assert.Empty(monitor.Tick(true, () =>
        {
            calls++;
            return [Fps(90)];
        }, null, Now));
        Assert.Equal(0, calls);

        monitor.Start();
        for (var i = 0; i < LivePerformanceMonitor.MaxPoints + 5; i++)
        {
            Assert.True(monitor.TryRecord([Fps(80 + i)], EvidenceKind.LiveSample, Now.AddSeconds(i)));
        }

        Assert.Equal(LivePerformanceMonitor.MaxPoints, monitor.History.Count);
        Assert.Equal(85, monitor.History[0].Metrics[0].Value);
        monitor.Stop();
        Assert.Empty(monitor.History);
        Assert.Equal(MonitorState.Stopped, monitor.State);
    }

    [Fact]
    public void IdleTickDoesNotCallTheHeadsetSampler()
    {
        var monitor = new LivePerformanceMonitor();
        monitor.Start();
        var calls = 0;
        var point = monitor.Tick(false, () =>
        {
            calls++;
            return [Fps(120)];
        }, [Fps(30)], Now);
        Assert.Equal(0, calls);
        Assert.Contains(point, metric => metric.Name == "RuntimeFps" && metric.Availability == MetricAvailability.Unavailable);
        Assert.Contains(point, metric => (metric.Note ?? string.Empty).Contains("not polled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActiveSessionSamplesOncePerIntervalAndKeepsMissingValuesBlank()
    {
        var monitor = new LivePerformanceMonitor();
        monitor.Start();
        var calls = 0;
        var first = monitor.Tick(true, () =>
        {
            calls++;
            return
            [
                TelemetryMetric.AvailableNumber("RuntimeFps", "FPS", 72, MetricQuality.Measured, "headset-logcat", "ALVR", Now, null),
                TelemetryMetric.Unavailable("HeadsetGpuClock", "MHz", "headset-logcat", "ALVR", Now, "Field was not present in the runtime sample.")
            ];
        }, null, Now);
        var second = monitor.Tick(true, () =>
        {
            calls++;
            return [Fps(1)];
        }, null, Now.AddSeconds(5));
        Assert.Equal(1, calls);
        Assert.Equal(72, first[0].Value);
        Assert.Contains("Runtime evidence", first[0].Note, StringComparison.Ordinal);
        Assert.Equal(MetricAvailability.Unavailable, first[1].Availability);
        Assert.Empty(second);
        Assert.False(monitor.HeadsetSampleDue(false, false, Now.AddMinutes(1)));
    }

    [Fact]
    public void PastedLogsStayDistinctFromPropertyReadback()
    {
        var pasted = LivePerformanceMonitor.Label(
            [TelemetryMetric.AvailableNumber("RuntimeFps", "FPS", 80, MetricQuality.Measured, "pasted-log", null, Now, null)],
            EvidenceKind.PastedLog,
            Now);
        var readback = LivePerformanceMonitor.Label(
            [TelemetryMetric.AvailableNumber("RefreshHz", "Hz", 120, MetricQuality.Measured, "getprop", null, Now, null)],
            EvidenceKind.PropertyReadback,
            Now);
        Assert.StartsWith("Pasted log", pasted[0].Note, StringComparison.Ordinal);
        Assert.StartsWith("Property readback", readback[0].Note, StringComparison.Ordinal);
        Assert.DoesNotContain("Live sample", pasted[0].Note, StringComparison.Ordinal);
    }

    private static TelemetryMetric Fps(double value) =>
        TelemetryMetric.AvailableNumber("RuntimeFps", "FPS", value, MetricQuality.Measured, "test", null, Now, null);
}
