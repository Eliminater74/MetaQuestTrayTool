using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class HeadsetTelemetryTests
{
    [Fact]
    public void ExtendedLogLineKeepsClocksSeparateFromLevelsAndDoesNotInventMissingFields()
    {
        var line = "VrApi: FPS=90.5, CPU=4, GPU=4, CPUUtil=40%, GPUUtil=97%, CPU Clock=1478 MHz, GPU Clock=640 MHz, Memory Clock=1700 MHz, Battery Temp=36.5 C, Thermal=0, Dropped=2, Reprojected=11, Refresh=120, Fov=2, ASW=Off";
        var sample = HeadsetPerformanceSample.Parse(line, TimeSpan.FromSeconds(10));
        var parsed = Assert.Single(sample.Samples);
        Assert.Equal(4, parsed.CpuLevel);
        Assert.Equal(4, parsed.GpuLevel);
        Assert.Equal(1478, parsed.CpuClockMHz);
        Assert.Equal(640, parsed.GpuClockMHz);
        Assert.Equal(1700, parsed.MemoryClockMHz);
        Assert.Equal(36.5, parsed.BatteryTemperatureC);
        Assert.Equal("0", parsed.ThermalStatus);
        Assert.Equal(2, parsed.DroppedFrames);
        Assert.Equal(11, parsed.ReprojectedFrames);
        Assert.Equal(120, parsed.RefreshHz);
        Assert.Contains("GPU clock MHz: 640", sample.ToDisplayText(), StringComparison.Ordinal);

        var when = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var metrics = HeadsetTelemetry.FromSample(sample, when, "MetaQuestLink");
        var fps = Assert.Single(metrics, metric => metric.Name == "RuntimeFps");
        Assert.Equal(MetricAvailability.Available, fps.Availability);
        Assert.Equal(MetricQuality.Measured, fps.Quality);
        Assert.Contains("not fresh-frame", fps.Note, StringComparison.Ordinal);
        Assert.Equal(640, Assert.Single(metrics, metric => metric.Name == "HeadsetGpuClock").Value);
        Assert.Equal(1478, Assert.Single(metrics, metric => metric.Name == "HeadsetCpuClock").Value);
    }

    [Fact]
    public void MalformedClockTextDoesNotBecomeASampleByItself()
    {
        var sample = HeadsetPerformanceSample.Parse("GPU Clock=fast Memory Clock= MHz", TimeSpan.FromSeconds(1));
        Assert.Empty(sample.Samples);
    }

    [Fact]
    public void HeadsetAdbStaysOffWhileIdleUnlessTheUserAsks()
    {
        Assert.False(TelemetryCollectionPolicy.AllowBackgroundAdbWhileIdle);
        Assert.False(TelemetryCollectionPolicy.AllowHeadsetAdb(pcvrSessionActive: false, userRequested: false));
        Assert.True(TelemetryCollectionPolicy.AllowHeadsetAdb(pcvrSessionActive: true, userRequested: false));
        Assert.True(TelemetryCollectionPolicy.AllowHeadsetAdb(pcvrSessionActive: false, userRequested: true));
    }
}
