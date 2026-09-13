using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class LinkSettingsTests
{
    [Fact]
    public void BitratePresetsExposeOdt960()
    {
        Assert.Contains(960, LinkSettings.BitratePresets);
        Assert.Equal(LinkSettings.BitratePresets.Order().Distinct(), LinkSettings.BitratePresets);
    }

    [Fact]
    public void HighOdtBitrateWritesAndReadsBack()
    {
        var registry = new FakeRegistry();
        var result = new LinkSettingsService(registry).Apply(new LinkSettings
        {
            BitrateMbps = 960,
            DynamicBitrateMax = 960
        }, true);

        Assert.True(result.Succeeded);
        Assert.Equal(960, registry.Values["BitrateMbps"]);
        Assert.Equal(960, registry.Values["DBRMax"]);
        Assert.Equal(960, result.Current!.BitrateMbps);
        Assert.Equal(960, result.Current.DynamicBitrateMax);
    }

    [Theory]
    [InlineData(500, 0, 960, 0, 960, 0, true)]
    [InlineData(500, 500, 500, 960, 500, 960, true)]
    [InlineData(500, 500, 960, 960, 960, 960, true)]
    [InlineData(700, 0, 960, 0, 700, 0, false)]
    [InlineData(0, 700, 0, 960, 0, 700, false)]
    [InlineData(500, 500, 500, 500, 500, 500, false)]
    public void StartupPreservesExternalHighOdtBitratesOverLegacySavedBaseline(
        int savedBitrate,
        int savedDynamicMax,
        int currentBitrate,
        int currentDynamicMax,
        int expectedBitrate,
        int expectedDynamicMax,
        bool expectedSummary)
    {
        var saved = new LinkSettings
        {
            PresetName = "Saved",
            BitrateMbps = savedBitrate,
            DynamicBitrateMax = savedDynamicMax,
            EncodeResolutionWidth = 2912,
            Sharpening = LinkSharpeningMode.Quality
        };
        var current = new LinkSettings
        {
            BitrateMbps = currentBitrate,
            DynamicBitrateMax = currentDynamicMax
        };

        var resolved = LinkSettingsService.PreserveExternalHighBitratesForStartup(saved, current, out var summary);

        Assert.Equal(expectedBitrate, resolved.BitrateMbps);
        Assert.Equal(expectedDynamicMax, resolved.DynamicBitrateMax);
        Assert.Equal(2912, resolved.EncodeResolutionWidth);
        Assert.Equal(LinkSharpeningMode.Quality, resolved.Sharpening);
        Assert.Equal(expectedSummary, summary is not null);
        Assert.Equal(savedBitrate, saved.BitrateMbps);
        Assert.Equal(savedDynamicMax, saved.DynamicBitrateMax);
    }

    [Fact]
    public void StartupPreflightReadFailureBlocksAutomaticLinkApply()
    {
        var registry = new FakeRegistry { FailRead = true };
        registry.Values["BitrateMbps"] = 960;
        registry.Values["DBRMax"] = 960;
        var service = new LinkSettingsService(registry);
        var saved = new LinkSettings
        {
            BitrateMbps = 500,
            DynamicBitrateMax = 500
        };

        var preflight = service.PreflightStartupLinkApply(saved);
        if (preflight.CanApplyLinkSettings)
        {
            service.Apply(preflight.ResolvedSettings, deleteUnsetOverrides: true);
        }

        Assert.False(preflight.CanApplyLinkSettings);
        Assert.False(preflight.ShouldSaveResolvedSettings);
        Assert.Same(saved, preflight.ResolvedSettings);
        Assert.Contains("Skipped startup Link apply", preflight.Summary);
        Assert.Equal(960, registry.Values["BitrateMbps"]);
        Assert.Equal(960, registry.Values["DBRMax"]);
        Assert.Equal(1, registry.ReadOpens);
        Assert.Null(service.LastResult);
    }

    [Fact]
    public void StartupPreflightAllowsApplyWhenReadSucceedsWithoutPreservation()
    {
        var registry = new FakeRegistry();
        registry.Values["BitrateMbps"] = 500;
        registry.Values["DBRMax"] = 500;
        var saved = new LinkSettings
        {
            BitrateMbps = 500,
            DynamicBitrateMax = 500
        };

        var preflight = new LinkSettingsService(registry).PreflightStartupLinkApply(saved);

        Assert.True(preflight.CanApplyLinkSettings);
        Assert.False(preflight.ShouldSaveResolvedSettings);
        Assert.Same(saved, preflight.ResolvedSettings);
        Assert.Null(preflight.Summary);
    }

    [Fact]
    public void NegativeDynamicOffsetSurvivesApplyAndUnrelatedEdits()
    {
        var registry = new FakeRegistry();
        registry.Values["DBROffsetMbps"] = -25;
        var service = new LinkSettingsService(registry);
        var settings = service.ReadCurrent();
        settings.BitrateMbps = 450;
        var result = service.Apply(settings, true);
        Assert.True(result.Succeeded);
        Assert.Equal(-25, result.Current!.DynamicBitrateOffsetMbps);
        Assert.Equal(-25, registry.Values["DBROffsetMbps"]);
    }
    [Theory]
    [InlineData(2912)]
    [InlineData(0)]
    public void OdtWidthIncludingAutoBeatsStaleLegacyAlias(int odtWidth)
    {
        var registry = new FakeRegistry();
        registry.Values["EncodeWidth"] = odtWidth;
        registry.Values["EncodeResolutionWidth"] = 3664;
        Assert.Equal(odtWidth, new LinkSettingsService(registry).ReadCurrent().EncodeResolutionWidth);
    }

    [Fact]
    public void LegacyWidthRemainsReadableWhenOdtWidthIsAbsent()
    {
        var registry = new FakeRegistry();
        registry.Values["EncodeResolutionWidth"] = 3664;
        Assert.Equal(3664, new LinkSettingsService(registry).ReadCurrent().EncodeResolutionWidth);
    }

    [Theory]
    [InlineData(1, LinkSharpeningMode.Disabled)]
    [InlineData(2, LinkSharpeningMode.Normal)]
    [InlineData(3, LinkSharpeningMode.Quality)]
    public void SharpeningMatchesObservedOdtDwords(int value, LinkSharpeningMode mode)
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        registry.Values["LinkSharpeningEnabled"] = value;
        Assert.Equal(mode, service.ReadCurrent().Sharpening);
        registry.Values.Clear();
        Assert.True(service.Apply(new LinkSettings { Sharpening = mode }, true).Succeeded);
        Assert.Equal(value, registry.Values["LinkSharpeningEnabled"]);
    }

    [Fact]
    public void MissingRegistryAndMissingSharpeningAreDefaultsNotDisabled()
    {
        var registry = new FakeRegistry { Missing = true };
        var service = new LinkSettingsService(registry);
        Assert.Equal(LinkSharpeningMode.Default, service.ReadCurrent().Sharpening);
        registry.Missing = false;
        Assert.Equal(LinkSharpeningMode.Default, service.ReadCurrent().Sharpening);
    }

    [Fact]
    public void ApplyMapsAllOverridesAndReadsBackFreshValues()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        var settings = DistinctiveSettings();
        var result = service.Apply(settings, true);
        Assert.True(result.Succeeded);
        Assert.Equal(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["BitrateMbps"] = 500, ["EncodeWidth"] = 2912, ["EncodeResolutionWidth"] = 2912,
            ["DBR"] = 1, ["DBRMax"] = 350, ["DBROffsetMbps"] = 25, ["HEVC"] = 1,
            ["NumSlices"] = 1, ["LinkSharpeningEnabled"] = 3, ["DistortionCurve"] = 0, ["MobileASWMode"] = 1
        }.OrderBy(p => p.Key), registry.Values.OrderBy(p => p.Key));
        Assert.Equal(500, result.Current!.BitrateMbps);
        Assert.Equal(2912, result.Current.EncodeResolutionWidth);
        Assert.Equal(EncodeDynamicBitrateMode.Enabled, result.Current.EncodeDynamicBitrate);
        Assert.Equal(350, result.Current.DynamicBitrateMax);
        Assert.Equal(25, result.Current.DynamicBitrateOffsetMbps);
        Assert.True(result.Current.PreferHevc);
        Assert.True(result.Current.DisableSlicedEncoding);
        Assert.Equal(DistortionCurvature.Low, result.Current.DistortionCurvature);
        Assert.Equal(MobileAswMode.Enabled, result.Current.MobileAsw);
        Assert.Equal(LinkSharpeningMode.Quality, result.Current.Sharpening);
        Assert.Equal(1, registry.ReadOpens);
        Assert.Contains("ODT/runtime application is not verified", result.Summary);
        settings.BitrateMbps = 100;
        Assert.Equal(500, service.LastApplied!.BitrateMbps);
        Assert.Equal(500, result.Written!.BitrateMbps);
    }

    [Fact]
    public void GlobalDefaultsDeleteBothWidthNamesAndAllOwnedOverridesOnly()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        service.Apply(DistinctiveSettings(), true);
        registry.Values["UnrelatedMetaSetting"] = 42;
        Assert.True(service.Apply(new LinkSettings(), true).Succeeded);
        var remaining = Assert.Single(registry.Values);
        Assert.Equal("UnrelatedMetaSetting", remaining.Key);
        Assert.Equal(LinkSharpeningMode.Default, service.ReadCurrent().Sharpening);
    }

    [Fact]
    public void NonDeletingDefaultsPreserveEnumOverridesAndClearNumericOverridesToZero()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        service.Apply(DistinctiveSettings(), true);
        var result = service.Apply(new LinkSettings(), false);
        Assert.True(result.Succeeded);
        foreach (var name in new[] { "BitrateMbps", "EncodeWidth", "EncodeResolutionWidth", "DBRMax", "DBROffsetMbps" })
            Assert.Equal(0, registry.Values[name]);
        Assert.Equal(LinkSharpeningMode.Quality, result.Current!.Sharpening);
        Assert.Equal(DistortionCurvature.Low, result.Current.DistortionCurvature);
        Assert.Equal(EncodeDynamicBitrateMode.Enabled, result.Current.EncodeDynamicBitrate);
        Assert.Equal(MobileAswMode.Enabled, result.Current.MobileAsw);
        Assert.False(registry.Values.ContainsKey("HEVC"));
        Assert.False(registry.Values.ContainsKey("NumSlices"));
    }

    [Fact]
    public void ExplicitDisabledAndHighModesAreWrittenNotDeleted()
    {
        var registry = new FakeRegistry();
        var result = new LinkSettingsService(registry).Apply(new LinkSettings
        {
            EncodeDynamicBitrate = EncodeDynamicBitrateMode.Disabled,
            MobileAsw = MobileAswMode.Disabled,
            DistortionCurvature = DistortionCurvature.High
        }, true);
        Assert.True(result.Succeeded);
        Assert.Equal(0, registry.Values["DBR"]);
        Assert.Equal(0, registry.Values["MobileASWMode"]);
        Assert.Equal(1, registry.Values["DistortionCurve"]);
    }

    [Fact]
    public void RewrittenBitrateFailsWithRequestedAndObservedValues()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        Assert.True(service.Apply(DistinctiveSettings(), true).Succeeded);
        registry.BeforeRead = () => registry.Values["BitrateMbps"] = 450;
        var result = service.Apply(DistinctiveSettings(), true);
        Assert.False(result.Succeeded);
        Assert.Null(service.LastApplied);
        Assert.Equal(450, result.Current!.BitrateMbps);
        Assert.Contains("BitrateMbps: requested DWORD 500, read back DWORD 450", result.Mismatches);
    }

    [Fact]
    public void WrongRegistryTypeDoesNotPassNumericReadBack()
    {
        var registry = new FakeRegistry();
        registry.BeforeRead = () => registry.Values["BitrateMbps"] = 500L;
        var result = new LinkSettingsService(registry).Apply(DistinctiveSettings(), true);
        Assert.False(result.Succeeded);
        Assert.Contains("non-DWORD value", result.Summary);
    }

    [Fact]
    public void FailedDeletionAndAliasWriteAreDetectedIndependently()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        service.Apply(DistinctiveSettings(), true);
        registry.IgnoreDeletes = true;
        Assert.False(service.Apply(new LinkSettings(), true).Succeeded);
        registry.BeforeRead = () => registry.Values["EncodeResolutionWidth"] = 3664;
        var result = service.Apply(DistinctiveSettings(), true);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Mismatches, m => m.StartsWith("EncodeResolutionWidth:"));
        Assert.Equal(2912, result.Current!.EncodeResolutionWidth);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AccessFailuresReturnFailureWithoutRetryingTheFailingRead(bool failWrite)
    {
        var registry = new FakeRegistry { FailWrite = failWrite, FailRead = !failWrite };
        var service = new LinkSettingsService(registry);
        var result = service.Apply(DistinctiveSettings(), true);
        Assert.False(result.Succeeded);
        Assert.Null(result.Current);
        Assert.Contains("denied", result.Summary);
        Assert.Equal(failWrite ? 0 : 1, registry.ReadOpens);
    }

    [Fact]
    public void ReadCurrentObservesExternalChangesAndCaseInsensitiveSliceName()
    {
        var registry = new FakeRegistry();
        var service = new LinkSettingsService(registry);
        service.Apply(DistinctiveSettings(), true);
        registry.Values["BitrateMbps"] = 450;
        registry.Values["numSlices"] = 1;
        Assert.Equal(450, service.ReadCurrent().BitrateMbps);
        Assert.True(service.ReadCurrent().DisableSlicedEncoding);
        Assert.Equal(500, service.LastApplied!.BitrateMbps);
    }

    [Theory]
    [InlineData(VrConnectionKind.VirtualDesktop, false)]
    [InlineData(VrConnectionKind.SteamLinkOrSteamVr, false)]
    [InlineData(VrConnectionKind.MetaAirLink, true)]
    [InlineData(VrConnectionKind.MetaWiredLink, true)]
    public void ExistingSessionPolicyStillProtectsBothMetaPipelines(VrConnectionKind kind, bool allowed)
    {
        var caps = VrSessionCapabilities.From(new VrConnectionStatus { Kind = kind, SessionActive = true, Summary = "test session" });
        Assert.Equal(allowed, caps.AllowsMetaLinkRegistry);
        Assert.Equal(allowed, caps.AllowsOculusDebugTool);
    }

    private static LinkSettings DistinctiveSettings() => new()
    {
        BitrateMbps = 500, EncodeResolutionWidth = 2912,
        EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled, DynamicBitrateMax = 350,
        DynamicBitrateOffsetMbps = 25, PreferHevc = true, DisableSlicedEncoding = true,
        Sharpening = LinkSharpeningMode.Quality, DistortionCurvature = DistortionCurvature.Low,
        MobileAsw = MobileAswMode.Enabled
    };

    private sealed class FakeRegistry : ILinkSettingsRegistry, ILinkSettingsRegistryKey
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Action? BeforeRead { get; set; }
        public bool Missing { get; set; }
        public bool FailWrite { get; set; }
        public bool FailRead { get; set; }
        public bool IgnoreDeletes { get; set; }
        public int ReadOpens { get; private set; }

        public ILinkSettingsRegistryKey? Open(bool writable)
        {
            if (writable)
            {
                if (FailWrite) throw new UnauthorizedAccessException("Write denied");
                Missing = false;
            }
            else
            {
                ReadOpens++;
                if (FailRead) throw new UnauthorizedAccessException("Read denied");
                BeforeRead?.Invoke();
            }
            return Missing ? null : this;
        }
        public object? GetValue(string name) => Values.GetValueOrDefault(name);
        public void SetValue(string name, int value) => Values[name] = value;
        public void DeleteValue(string name) { if (!IgnoreDeletes) Values.Remove(name); }
        public void Dispose() { }
    }
}
