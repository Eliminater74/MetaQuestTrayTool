using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class ExperimentalGpuPolicyTests
{
    [Fact]
    public void StandardLevelsStayUnchangedWhenExperimentalIsOff()
    {
        var settings = new HeadsetSettings
        {
            CpuLevel = HeadsetPerformanceLevel.Level4,
            GpuLevel = HeadsetPerformanceLevel.Level4,
            ExperimentalGpuMaximumEnabled = false
        };
        var commands = HeadsetSettingsService.PerformanceOverridesForApply(settings, ExperimentalGpuPolicy.Evaluate(false, "Quest 3", 40));
        Assert.Equal("4", commands["debug.oculus.cpuLevel"]);
        Assert.Equal("4", commands["debug.oculus.gpuLevel"]);
        settings.GpuLevel = (HeadsetPerformanceLevel)5;
        Assert.Throws<InvalidOperationException>(() => HeadsetSettingsService.PerformanceOverrides(settings));
    }

    [Theory]
    [InlineData("Quest 3S", 30)]
    [InlineData("Quest 2", 30)]
    [InlineData("Quest Pro", 30)]
    [InlineData("Unknown", 30)]
    [InlineData(null, 30)]
    public void NonQuest3ModelsAreRefused(string? model, double temperature)
    {
        var decision = ExperimentalGpuPolicy.Evaluate(true, model, temperature);
        Assert.False(decision.ApplyLevel7);
        Assert.False(string.IsNullOrWhiteSpace(decision.Refusal));
    }

    [Fact]
    public void Quest3AtFortyFiveCelsiusIsRefusedAndBelowIsAllowedWithoutClaimingAClock()
    {
        var hot = ExperimentalGpuPolicy.Evaluate(true, "Meta Quest 3", 45);
        Assert.False(hot.ApplyLevel7);
        Assert.Contains("45", hot.Refusal, StringComparison.Ordinal);

        var warm = ExperimentalGpuPolicy.Evaluate(true, "Quest 3", 44.9);
        Assert.True(warm.ApplyLevel7);
        Assert.Contains("clock", warm.Warning, StringComparison.OrdinalIgnoreCase);

        var unknown = ExperimentalGpuPolicy.Evaluate(true, "Quest 3", null);
        Assert.True(unknown.ApplyLevel7);
        Assert.Contains("not available", unknown.Warning, StringComparison.OrdinalIgnoreCase);

        var settings = new HeadsetSettings
        {
            GpuLevel = HeadsetPerformanceLevel.Level4,
            ExperimentalGpuMaximumEnabled = true
        };
        var commands = HeadsetSettingsService.PerformanceOverridesForApply(settings, warm);
        Assert.Equal("7", commands["debug.oculus.gpuLevel"]);
        Assert.False(commands.ContainsKey("debug.oculus.cpuLevel"));
    }

    [Fact]
    public void ObservationKeepsRequestedReadbackRuntimeClockAndThermalSeparate()
    {
        var text = ExperimentalGpuPolicy.DescribeObservation(7, "7", 4, null, 96, "status 0");
        Assert.Contains("Requested GPU level: 7", text, StringComparison.Ordinal);
        Assert.Contains("Property readback: 7", text, StringComparison.Ordinal);
        Assert.Contains("Runtime-reported GPU level: 4", text, StringComparison.Ordinal);
        Assert.Contains("GPU clock: not measured", text, StringComparison.Ordinal);
        Assert.Contains("96%", text, StringComparison.Ordinal);
        Assert.Contains("status 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedFlagDefaultsOffAndRoundTrips()
    {
        var settings = new HeadsetSettings();
        Assert.False(settings.ExperimentalGpuMaximumEnabled);
        settings.ExperimentalGpuMaximumEnabled = true;
        var copy = System.Text.Json.JsonSerializer.Deserialize<HeadsetSettings>(System.Text.Json.JsonSerializer.Serialize(settings));
        Assert.True(copy!.ExperimentalGpuMaximumEnabled);
    }
}
