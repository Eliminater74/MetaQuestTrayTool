using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class HeadsetPerformanceSampleTests
{
    [Theory]
    [InlineData("VrRuntime: CPUUtil=82%,GPUUtil=91%")]
    [InlineData("OVRPlugin: SpaceWarp=Off")]
    public void ParsesRuntimeMetricsWithoutFpsOrVrApi(string line)
    {
        Assert.Single(HeadsetPerformanceSample.Parse(line, TimeSpan.FromSeconds(10)).Samples);
    }

    [Fact]
    public void LogcatStartsAtDeviceTimeWithoutClearingHistory()
    {
        Assert.Contains("date +%s.%N", AdbService.PerformanceLogcatCommand);
        Assert.Contains("logcat -T \"$start\"", AdbService.PerformanceLogcatCommand);
        Assert.DoesNotContain(" -c", AdbService.PerformanceLogcatCommand);
    }
    [Fact]
    public void ParsesVrApiFpsFoveationAndUtilizationFields()
    {
        var logcat = string.Join(Environment.NewLine,
            "09-10 12:00:01.000 I/VrApi: FPS=71.8,Prd=72,CPU=4,GPU=3,CPUUtil=82%,GPUUtil=91%,Fov=3D,ASW=Auto",
            "09-10 12:00:02.000 I/VrApi: FPS=72.0,Prd=72,CPU=4,GPU=3,CPUUtil=78%,GPUUtil=87%,Fov=2,SpaceWarp=Off");

        var sample = HeadsetPerformanceSample.Parse(logcat, TimeSpan.FromSeconds(10));
        var text = sample.ToDisplayText();

        Assert.Equal(2, sample.Samples.Count);
        Assert.Contains("FPS: last 72", text);
        Assert.Contains("CPU level: 4", text);
        Assert.Contains("GPU level: 3", text);
        Assert.Contains("CPU utilization: 78%", text);
        Assert.Contains("GPU utilization: 87%", text);
        Assert.Contains("FFR/Foveation: 2", text);
        Assert.Contains("SpaceWarp: Off", text);
    }

    [Fact]
    public void ReportsNoSamplesWhenLogcatHasNoRuntimeStats()
    {
        var sample = HeadsetPerformanceSample.Parse("09-10 12:00:01.000 I/Other: hello", TimeSpan.FromSeconds(10));
        var text = sample.ToDisplayText();

        Assert.Empty(sample.Samples);
        Assert.Contains("No FPS", text);
        Assert.Contains("runtime logcat stats", text);
    }
}
