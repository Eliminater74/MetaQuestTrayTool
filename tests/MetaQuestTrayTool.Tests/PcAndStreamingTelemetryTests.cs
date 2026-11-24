using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PcAndStreamingTelemetryTests
{
    [Fact]
    public void NvidiaCsvParsesTheFirstGpuAndLeavesSteamVrFrameTimeUnavailable()
    {
        var when = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        var metrics = PcTelemetry.FromProbe("65, 72, 2100, 4096, 16384, 12\n10, 40, 300, 100, 8000, 0", null, 22.5, when);
        Assert.Equal(65, Assert.Single(metrics, metric => metric.Name == "PcGpuUtilization").Value);
        Assert.Equal(72, Assert.Single(metrics, metric => metric.Name == "PcGpuTemperature").Value);
        Assert.Equal(2100, Assert.Single(metrics, metric => metric.Name == "PcGpuClock").Value);
        Assert.Equal(12, Assert.Single(metrics, metric => metric.Name == "PcVideoEncoderUtilization").Value);
        Assert.Equal(22.5, Assert.Single(metrics, metric => metric.Name == "PcCpuUtilization").Value);
        Assert.Equal(MetricAvailability.Unavailable, Assert.Single(metrics, metric => metric.Name == "SteamVrFrameTime").Availability);
    }

    [Fact]
    public void MissingGpuProbeDoesNotInventUtilization()
    {
        var metrics = PcTelemetry.FromProbe(null, "nvidia-smi was not found.", null, DateTimeOffset.UnixEpoch);
        Assert.All(metrics.Where(metric => metric.Name != "PcCpuUtilization"), metric => Assert.Equal(MetricAvailability.Unavailable, metric.Availability));
        Assert.Contains("not found", Assert.Single(metrics, metric => metric.Name == "PcGpuUtilization").Note, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nvidia-smi")]
    [InlineData(@"C:\Tools\nvidia-smi.exe")]
    public void SafeProbeNamesAreAccepted(string name) => Assert.True(PcTelemetry.IsSafeExecutable(name));

    [Theory]
    [InlineData("nvidia-smi & calc")]
    [InlineData("cmd.exe")]
    [InlineData("")]
    public void UnsafeProbeNamesAreRejected(string name) => Assert.False(PcTelemetry.IsSafeExecutable(name));

    [Fact]
    public void StreamingParserKeepsEstimatesAndDoesNotTreatRuntimeFpsAsFreshFrames()
    {
        var text = "bitrate=150 Mbps, encode latency=2.5 ms, network latency=4 ms estimate, fresh fps=118.6";
        var metrics = StreamingTelemetry.Parse(text, "ALVR", DateTimeOffset.UnixEpoch);
        Assert.Equal(150, Assert.Single(metrics, metric => metric.Name == "StreamingBitrate").Value);
        Assert.Equal(MetricQuality.Estimated, Assert.Single(metrics, metric => metric.Name == "EncodeLatency").Quality);
        Assert.Equal(MetricQuality.Measured, Assert.Single(metrics, metric => metric.Name == "FreshFrameFps").Quality);
        Assert.Equal(MetricAvailability.Unavailable, Assert.Single(metrics, metric => metric.Name == "DecodeLatency").Availability);
        Assert.DoesNotContain(metrics, metric => metric.Name == "RuntimeFps");
    }

    [Fact]
    public void EmptyStreamingLogIsEntirelyUnavailable()
    {
        var metrics = StreamingTelemetry.Parse("FPS=72", "MetaQuestLink", DateTimeOffset.UnixEpoch);
        Assert.All(metrics, metric => Assert.Equal(MetricAvailability.Unavailable, metric.Availability));
    }
}
