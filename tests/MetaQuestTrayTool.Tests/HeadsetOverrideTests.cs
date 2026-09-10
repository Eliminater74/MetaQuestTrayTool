using System.Text.Json;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class HeadsetOverrideTests
{
    [Fact]
    public void OldCombinedSettingsSurviveAndIndependentDefaultsOverrideLegacy()
    {
        var old = JsonSerializer.Deserialize<HeadsetSettings>("{\"CpuGpuLevel\":2}")!;
        Assert.Equal(HeadsetPerformanceLevel.Level4, old.EffectiveCpuLevel);
        Assert.Equal(HeadsetPerformanceLevel.Level4, old.EffectiveGpuLevel);
        old.CpuLevel = HeadsetPerformanceLevel.AppDefault;
        old.GpuLevel = HeadsetPerformanceLevel.Level3;
        var roundTrip = JsonSerializer.Deserialize<HeadsetSettings>(JsonSerializer.Serialize(old))!;
        var commands = HeadsetSettingsService.PerformanceOverrides(roundTrip);
        Assert.Single(commands);
        Assert.Equal("3", commands["debug.oculus.gpuLevel"]);
    }

    [Fact]
    public void CpuFourGpuZeroAreIndependentAndUnknownLevelsRejected()
    {
        var settings = new HeadsetSettings { CpuLevel = HeadsetPerformanceLevel.Level4, GpuLevel = HeadsetPerformanceLevel.Level0 };
        var commands = HeadsetSettingsService.PerformanceOverrides(settings);
        Assert.Equal("4", commands["debug.oculus.cpuLevel"]);
        Assert.Equal("0", commands["debug.oculus.gpuLevel"]);
        settings.GpuLevel = (HeadsetPerformanceLevel)5;
        Assert.Throws<InvalidOperationException>(() => HeadsetSettingsService.PerformanceOverrides(settings));
    }

    [Theory]
    [InlineData("Quest Pro", HeadsetRefreshRate.Hz120, false)]
    [InlineData("Quest 3S", HeadsetRefreshRate.Hz60, false)]
    [InlineData("Quest 3S", HeadsetRefreshRate.Hz120, true)]
    [InlineData("Quest 2", HeadsetRefreshRate.Hz60, true)]
    [InlineData("Oculus Quest", HeadsetRefreshRate.Hz90, false)]
    [InlineData("Unknown", HeadsetRefreshRate.Hz72, false)]
    public void RefreshRatesRespectModel(string model, HeadsetRefreshRate rate, bool supported) =>
        Assert.Equal(supported, HeadsetCapabilities.RefreshRates(model).Contains(rate));
}
