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

    [Fact]
    public void DynamicDoesNotForceFixedLevelAndDefaultDoesNotWrite()
    {
        var settings = new HeadsetSettings { Ffr = HeadsetFfrLevel.High };
        Assert.Equal("3", HeadsetSettingsService.FoveationOverrides(settings)["debug.oculus.foveation.level"]);
        settings.FoveationMode = HeadsetFoveationMode.Dynamic;
        var dynamic = HeadsetSettingsService.FoveationOverrides(settings);
        Assert.Single(dynamic);
        Assert.Equal("1", dynamic["debug.oculus.foveation.dynamic"]);
        settings.FoveationMode = HeadsetFoveationMode.AppDefault;
        Assert.Empty(HeadsetSettingsService.FoveationOverrides(settings));
    }

    [Fact]
    public void ExperimentalOverridesGateQuestProLocalDimmingAndSubsampledFoveation()
    {
        var settings = new HeadsetSettings
        {
            LocalDimming = HeadsetExperimentalOverride.ForceOn,
            SubsampledFoveation = HeadsetExperimentalOverride.ForceOff
        };

        var questPro = HeadsetSettingsService.ExperimentalOverrides(settings, "Quest Pro");
        Assert.Equal("1", questPro["debug.oculus.localDimming"]);
        Assert.Equal("0", questPro["debug.oculus.foveation.subsampled"]);
        Assert.Throws<InvalidOperationException>(() => HeadsetSettingsService.ExperimentalOverrides(settings, "Quest 3"));

        settings.LocalDimming = HeadsetExperimentalOverride.AppDefault;
        var quest3 = HeadsetSettingsService.ExperimentalOverrides(settings, "Quest 3");
        Assert.Single(quest3);
        Assert.Equal("0", quest3["debug.oculus.foveation.subsampled"]);
    }

    [Fact]
    public void DocumentedResetDefaultsDoNotInventUnsafeClears()
    {
        var quest3 = HeadsetSettingsService.DocumentedDefaultOverrides("Quest 3S");
        Assert.Equal("1680", quest3["debug.oculus.textureWidth"]);
        Assert.Equal("1760", quest3["debug.oculus.textureHeight"]);
        Assert.Equal("72", quest3["debug.oculus.refreshRate"]);
        Assert.Equal("1024", quest3["debug.oculus.capture.width"]);
        Assert.Equal("1024", quest3["debug.oculus.capture.height"]);
        Assert.Equal("5000000", quest3["debug.oculus.capture.bitrate"]);
        Assert.Equal("0", quest3["debug.oculus.fullRateCapture"]);
        Assert.Equal("0", quest3["debug.oculus.enableVideoCapture"]);

        Assert.DoesNotContain("debug.oculus.cpuLevel", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.gpuLevel", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.foveation.level", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.foveation.dynamic", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.forceChroma", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.localDimming", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.foveation.subsampled", quest3.Keys);
        Assert.DoesNotContain("debug.oculus.capture.fps", quest3.Keys);

        var unknown = HeadsetSettingsService.DocumentedDefaultOverrides("Unknown");
        Assert.False(unknown.ContainsKey("debug.oculus.textureWidth"));
        Assert.True(unknown.ContainsKey("debug.oculus.capture.width"));
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
