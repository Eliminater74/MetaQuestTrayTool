using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class HeadsetCapabilityDiscoveryTests
{
    [Theory]
    [InlineData("Quest 3", HeadsetFamily.Quest3)]
    [InlineData("Meta Quest 3", HeadsetFamily.Quest3)]
    [InlineData("Quest_3", HeadsetFamily.Quest3)]
    [InlineData("Quest 3S", HeadsetFamily.Quest3S)]
    [InlineData("Meta Quest 3S", HeadsetFamily.Quest3S)]
    [InlineData("Quest Pro", HeadsetFamily.QuestPro)]
    [InlineData("Quest 2", HeadsetFamily.Quest2)]
    [InlineData("Oculus Quest", HeadsetFamily.Quest1)]
    [InlineData("Quest 1", HeadsetFamily.Quest1)]
    [InlineData("", HeadsetFamily.Unknown)]
    [InlineData("Pico 4", HeadsetFamily.Unknown)]
    public void FamilyDoesNotTreatQuest3SAsQuest3(string model, HeadsetFamily expected) =>
        Assert.Equal(expected, HeadsetFamilyClassifier.Classify(model));

    [Fact]
    public void Quest3DocumentsStandardRatesAndMarksGpuLevel7Experimental()
    {
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe { ModelFallback = "Quest 3" });
        Assert.Equal(HeadsetFamily.Quest3, report.Family);
        Assert.Equal([72, 80, 90, 120], report.StandardRefreshRatesHz);
        Assert.Equal(CapabilityState.DocumentedModel, report.StandardRefreshSource);
        Assert.Equal(CapabilityState.Experimental, report.GpuLevel7);
        Assert.Equal(CapabilityState.Unknown, report.CurrentRefreshState);
        Assert.Contains("No getprop dump", report.ToDisplayText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Quest3SDoesNotInheritExperimentalGpuLevel7()
    {
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe { ModelFallback = "Meta Quest 3S" });
        Assert.Equal(HeadsetFamily.Quest3S, report.Family);
        Assert.Contains(120, report.StandardRefreshRatesHz);
        Assert.Equal(CapabilityState.Unsupported, report.GpuLevel7);
    }

    [Theory]
    [InlineData("Quest Pro", false)]
    [InlineData("Quest 2", true)]
    [InlineData("Oculus Quest", false)]
    [InlineData("Unknown Headset", false)]
    public void StandardRatesFollowExistingModelTable(string model, bool includes120)
    {
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe { ModelFallback = model });
        Assert.Equal(includes120, report.StandardRefreshRatesHz.Contains(120));
        if (model.StartsWith("Unknown", StringComparison.Ordinal))
        {
            Assert.Empty(report.StandardRefreshRatesHz);
            Assert.Equal(CapabilityState.Unknown, report.GpuLevel7);
        }
    }

    [Fact]
    public void LiveDumpOverridesModelAndParsesRefreshTemperatureAndFingerprint()
    {
        var dump = """
            [ro.product.model]: [Quest 3]
            [ro.build.version.release]: [14]
            [ro.build.fingerprint]: [oculus/eureka/eureka:14/UQ1A.240205.004/123:user/release-keys]
            [ro.oculus.build.version]: [74.0.0.0]
            [debug.oculus.gpuLevel]: [4]
            not a property
            [broken
            """;
        var display = "mActiveMode refreshRate=120.0 2064x2208";
        var battery = "AC powered: false\n  temperature: 351\n";
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe
        {
            Serial = "  SERIAL1  ",
            ModelFallback = "Quest 2",
            GetPropDump = dump,
            DisplayDump = display,
            BatteryDump = battery
        });

        Assert.Equal("SERIAL1", report.Serial);
        Assert.Equal(HeadsetFamily.Quest3, report.Family);
        Assert.Equal("14", report.AndroidVersion);
        Assert.Equal("74.0.0.0", report.HorizonOsVersion);
        Assert.Equal("ro.oculus.build.version", report.HorizonOsSource);
        Assert.Equal(120, report.CurrentRefreshHz);
        Assert.Equal(CapabilityState.Verified, report.CurrentRefreshState);
        Assert.Contains("2064x2208", report.ObservedDisplayModes);
        Assert.Equal(35.1, report.BatteryTemperatureC);
        Assert.Equal("4", report.GpuLevelProperty);
        Assert.Equal(CapabilityState.Verified, report.GpuLevelPropertyState);
        Assert.Equal(CapabilityState.Experimental, report.GpuLevel7);
    }

    [Fact]
    public void MalformedAndMissingDumpsStayUnknownWithoutThrowing()
    {
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe
        {
            ModelFallback = "Quest 3",
            GetPropDump = "garbage\n[ro.build.version.release]: []\n",
            DisplayDump = "refreshRate=not-a-number fps=5000",
            BatteryDump = "temperature: cold"
        });
        Assert.Equal(HeadsetFamily.Quest3, report.Family);
        Assert.Null(report.AndroidVersion);
        Assert.Null(report.CurrentRefreshHz);
        Assert.Null(report.BatteryTemperatureC);
        Assert.Equal(CapabilityState.Unknown, report.ThermalState);
    }

    [Fact]
    public void UnavailableAdbIsTemporarilyUnavailableAndKeepsModelFallback()
    {
        var report = HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe
        {
            ModelFallback = "Quest 2",
            AdbUnavailable = true,
            AdbError = "no device"
        });
        Assert.Equal(HeadsetFamily.Quest2, report.Family);
        Assert.Equal(CapabilityState.TemporarilyUnavailable, report.CurrentRefreshState);
        Assert.Equal(CapabilityState.TemporarilyUnavailable, report.ThermalState);
        Assert.Equal(CapabilityState.TemporarilyUnavailable, report.PerformanceTelemetry);
        Assert.Equal(CapabilityState.Unsupported, report.GpuLevel7);
    }

    [Fact]
    public void CacheInvalidatesWhenIdentityOrFingerprintChanges()
    {
        var cache = new HeadsetCapabilityCache();
        var calls = 0;
        HeadsetCapabilityReport Factory(string serial, string? fingerprint) =>
            HeadsetCapabilityDiscovery.Discover(new HeadsetAdbProbe
            {
                Serial = serial,
                ModelFallback = "Quest 3",
                GetPropDump = fingerprint is null ? null : $"[ro.build.fingerprint]: [{fingerprint}]"
            });

        var first = cache.GetOrAdd("A", "fp1", () =>
        {
            calls++;
            return Factory("A", "fp1");
        });
        var second = cache.GetOrAdd("A", "fp1", () =>
        {
            calls++;
            return Factory("A", "fp1");
        });
        Assert.Same(first, second);
        Assert.Equal(1, calls);

        cache.GetOrAdd("B", "fp1", () =>
        {
            calls++;
            return Factory("B", "fp1");
        });
        cache.GetOrAdd("B", "fp2", () =>
        {
            calls++;
            return Factory("B", "fp2");
        });
        cache.Invalidate();
        cache.GetOrAdd("B", "fp2", () =>
        {
            calls++;
            return Factory("B", "fp2");
        });
        Assert.Equal(4, calls);
    }
}
