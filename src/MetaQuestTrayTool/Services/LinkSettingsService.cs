using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

internal sealed record LinkStartupApplyPreflightResult(
    bool CanApplyLinkSettings,
    bool ShouldSaveResolvedSettings,
    LinkSettings ResolvedSettings,
    string? Summary);

/// <summary>
/// Reads and writes Quest Link / Air Link overrides in the Meta RemoteHeadset registry hive.
/// Persistence verification is separate from runtime/headset verification (see docs/ODT-REGISTRY.md).
/// </summary>
public sealed class LinkSettingsService
{
    public const string RegistryPath = @"Software\Oculus\RemoteHeadset";

    // ODT registry mappings and verification scope are documented in docs/ODT-REGISTRY.md.
    private const string BitrateValue = "BitrateMbps";
    private const string EncodeWidthOdtValue = "EncodeWidth";
    private const string EncodeWidthRuntimeValue = "EncodeResolutionWidth";
    private const string HevcValue = "HEVC";
    private const string NumSlicesValue = "NumSlices";
    private const string SharpeningValue = "LinkSharpeningEnabled";
    private const string DistortionValue = "DistortionCurve";
    private const string DynamicBitrateValue = "DBR";
    private const string DynamicBitrateMaxValue = "DBRMax";
    private const string DynamicBitrateOffsetValue = "DBROffsetMbps";
    private const string MobileAswValue = "MobileASWMode";

    private readonly ILinkSettingsRegistry _registry;

    public LinkSettingsService() : this(new LinkSettingsRegistry()) { }

    public LinkSettingsService(ILinkSettingsRegistry registry) => _registry = registry;

    public LinkSettings? LastApplied { get; private set; }
    public LinkApplyResult? LastResult { get; private set; }

    internal LinkStartupApplyPreflightResult PreflightStartupLinkApply(LinkSettings saved)
    {
        try
        {
            var resolved = PreserveExternalHighBitratesForStartup(saved, ReadCurrent(), out var summary);
            return new LinkStartupApplyPreflightResult(
                CanApplyLinkSettings: true,
                ShouldSaveResolvedSettings: summary is not null,
                ResolvedSettings: resolved,
                Summary: summary);
        }
        catch (Exception ex)
        {
            return new LinkStartupApplyPreflightResult(
                CanApplyLinkSettings: false,
                ShouldSaveResolvedSettings: false,
                ResolvedSettings: saved,
                Summary: "Skipped startup Link apply because existing ODT Link bitrate/DBRMax could not be checked: "
                         + ex.Message
                         + ". This avoids overwriting an unknown high ODT value.");
        }
    }

    internal static LinkSettings PreserveExternalHighBitratesForStartup(
        LinkSettings saved,
        LinkSettings current,
        out string? summary)
    {
        summary = null;
        var preserveBitrate = ShouldPreserveExternalHighBitrate(saved.BitrateMbps, current.BitrateMbps);
        var preserveDynamicMax = ShouldPreserveExternalHighBitrate(saved.DynamicBitrateMax, current.DynamicBitrateMax);
        if (!preserveBitrate && !preserveDynamicMax)
        {
            return saved;
        }

        var merged = saved.Clone();
        var fields = new List<string>();
        if (preserveBitrate)
        {
            merged.BitrateMbps = current.BitrateMbps;
            fields.Add($"bitrate {current.BitrateMbps} Mbps");
        }

        if (preserveDynamicMax)
        {
            merged.DynamicBitrateMax = current.DynamicBitrateMax;
            fields.Add($"DBR max {current.DynamicBitrateMax} Mbps");
        }

        merged.PresetName = "Custom";
        summary = "Preserved existing ODT Link " + string.Join(" and ", fields)
                  + " instead of applying the older saved baseline.";
        return merged;
    }

    private static bool ShouldPreserveExternalHighBitrate(int saved, int current) =>
        current > LinkSettings.LegacyBitratePresetCeilingMbps
        && saved <= LinkSettings.LegacyBitratePresetCeilingMbps;

    public LinkSettings ReadCurrent()
    {
        using var key = _registry.Open(writable: false);
        return ReadCurrent(key);
    }

    private static LinkSettings ReadCurrent(ILinkSettingsRegistryKey? key)
    {
        if (key is null)
        {
            return new LinkSettings();
        }

        // ODT edits EncodeWidth only. An explicit zero must also beat a stale legacy alias.
        var encodeWidth = key.GetValue(EncodeWidthOdtValue) is int odtWidth
            ? odtWidth
            : ReadDword(key, EncodeWidthRuntimeValue);

        return new LinkSettings
        {
            BitrateMbps = ReadDword(key, BitrateValue),
            EncodeResolutionWidth = encodeWidth,
            PreferHevc = ReadDword(key, HevcValue) == 1,
            DisableSlicedEncoding = ReadDword(key, NumSlicesValue) == 1,
            DistortionCurvature = ReadDistortion(key),
            EncodeDynamicBitrate = ReadDynamicBitrate(key),
            DynamicBitrateMax = ReadDword(key, DynamicBitrateMaxValue),
            DynamicBitrateOffsetMbps = ReadDword(key, DynamicBitrateOffsetValue),
            MobileAsw = ReadMobileAsw(key),
            Sharpening = key.GetValue(SharpeningValue) switch
            {
                1 => LinkSharpeningMode.Disabled,
                2 => LinkSharpeningMode.Normal,
                3 => LinkSharpeningMode.Quality,
                _ => LinkSharpeningMode.Default
            }
        };
    }

    public LinkApplyResult Apply(LinkSettings settings, bool deleteUnsetOverrides = false)
    {
        try
        {
            using var key = new LinkRegistryWriteBatch(_registry.Open(writable: true)
                            ?? throw new InvalidOperationException($"Could not open HKCU\\{RegistryPath}."));

            WriteOrClear(key, BitrateValue, settings.BitrateMbps, deleteUnsetOverrides);
            WriteEncodeWidth(key, settings.EncodeResolutionWidth, deleteUnsetOverrides);

            if (settings.PreferHevc)
            {
                key.SetValue(HevcValue, 1);
            }
            else
            {
                key.DeleteValue(HevcValue);
            }

            WriteSlicedEncoding(key, settings.DisableSlicedEncoding);
            WriteDistortion(key, settings.DistortionCurvature, deleteUnsetOverrides);
            WriteDynamicBitrate(key, settings.EncodeDynamicBitrate, deleteUnsetOverrides);
            WriteOrClear(key, DynamicBitrateMaxValue, settings.DynamicBitrateMax, deleteUnsetOverrides);
            WriteOrClear(key, DynamicBitrateOffsetValue, settings.DynamicBitrateOffsetMbps, deleteUnsetOverrides, allowNegative: true);
            WriteMobileAsw(key, settings.MobileAsw, deleteUnsetOverrides);
            WriteSharpening(key, settings.Sharpening, deleteUnsetOverrides);

            using var readBack = _registry.Open(writable: false);
            var mismatches = key.Verify(readBack);
            var current = ReadCurrent(readBack);
            var verified = mismatches.Count == 0;
            LastApplied = verified ? settings.Clone() : null;
            LastResult = new LinkApplyResult
            {
                Succeeded = verified,
                Written = settings.Clone(),
                Current = current,
                Mismatches = mismatches,
                Summary = verified
                    ? $"Verified Link registry overrides in HKCU\\{RegistryPath}: {current.Describe()}. "
                      + "ODT/runtime application is not verified by this read-back. Reopen ODT; reconnect Link or restart OVRService if needed."
                    : $"Link registry verification failed in HKCU\\{RegistryPath}: {string.Join("; ", mismatches)}. "
                      + "Some overrides may have changed; refresh before retrying."
            };
            SessionFlightRecorder.Mutation(
                "link-registry",
                verified ? "WRITE" : "WRITE-FAILED",
                $"bitrate={settings.BitrateMbps} dbr={settings.EncodeDynamicBitrate} dbrMax={settings.DynamicBitrateMax} "
                + $"encodeWidth={settings.EncodeResolutionWidth} hevc={settings.PreferHevc} "
                + $"verified={verified} current={current.Describe()}",
                nameof(LinkSettingsService));
            return LastResult;
        }
        catch (Exception ex)
        {
            LastApplied = null;
            // Do not repeat a failing registry read in the error handler.
            LastResult = new LinkApplyResult
            {
                Succeeded = false,
                Summary = $"Could not write/verify Link registry overrides in HKCU\\{RegistryPath}: {ex.Message}. Some overrides may have changed."
            };
            SessionFlightRecorder.Mutation(
                "link-registry",
                "WRITE-FAILED",
                LastResult.Summary,
                nameof(LinkSettingsService));
            return LastResult;
        }
    }

    public string DescribeRegistryStatus()
    {
        try
        {
            using var key = _registry.Open(writable: false);
            return key is null
                ? $"Registry key HKCU\\{RegistryPath} was not found yet."
                : $"Stored Link registry overrides: {ReadCurrent(key).Describe()}. Runtime state is not queried.";
        }
        catch (Exception ex)
        {
            return $"Could not read Link registry overrides: {ex.Message}";
        }
    }

    private static DistortionCurvature ReadDistortion(ILinkSettingsRegistryKey key)
    {
        if (key.GetValue(DistortionValue) is not int value)
        {
            return DistortionCurvature.Default;
        }

        return value switch
        {
            0 => DistortionCurvature.Low,
            1 => DistortionCurvature.High,
            _ => DistortionCurvature.Default
        };
    }

    private static EncodeDynamicBitrateMode ReadDynamicBitrate(ILinkSettingsRegistryKey key)
    {
        if (key.GetValue(DynamicBitrateValue) is not int value)
        {
            return EncodeDynamicBitrateMode.Default;
        }

        return value switch
        {
            0 => EncodeDynamicBitrateMode.Disabled,
            1 => EncodeDynamicBitrateMode.Enabled,
            _ => EncodeDynamicBitrateMode.Default
        };
    }

    private static MobileAswMode ReadMobileAsw(ILinkSettingsRegistryKey key)
    {
        if (key.GetValue(MobileAswValue) is not int value)
        {
            return MobileAswMode.Default;
        }

        return value switch
        {
            0 => MobileAswMode.Disabled,
            1 => MobileAswMode.Enabled,
            _ => MobileAswMode.Default
        };
    }

    private static void WriteEncodeWidth(ILinkSettingsRegistryKey key, int width, bool deleteWhenZero)
    {
        if (width > 0)
        {
            key.SetValue(EncodeWidthOdtValue, width);
            key.SetValue(EncodeWidthRuntimeValue, width);
            return;
        }

        if (deleteWhenZero)
        {
            key.DeleteValue(EncodeWidthOdtValue);
            key.DeleteValue(EncodeWidthRuntimeValue);
        }
        else
        {
            WriteOrClear(key, EncodeWidthOdtValue, 0, deleteWhenZero: false);
            WriteOrClear(key, EncodeWidthRuntimeValue, 0, deleteWhenZero: false);
        }
    }

    private static void WriteSlicedEncoding(ILinkSettingsRegistryKey key, bool disable)
    {
        if (disable)
        {
            key.SetValue(NumSlicesValue, 1);
        }
        else
        {
            key.DeleteValue(NumSlicesValue);
        }
    }

    private static void WriteDistortion(ILinkSettingsRegistryKey key, DistortionCurvature curvature, bool deleteWhenDefault)
    {
        switch (curvature)
        {
            case DistortionCurvature.Low:
                key.SetValue(DistortionValue, 0);
                break;
            case DistortionCurvature.High:
                key.SetValue(DistortionValue, 1);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(DistortionValue);
                }

                break;
        }
    }

    private static void WriteDynamicBitrate(ILinkSettingsRegistryKey key, EncodeDynamicBitrateMode mode, bool deleteWhenDefault)
    {
        switch (mode)
        {
            case EncodeDynamicBitrateMode.Disabled:
                key.SetValue(DynamicBitrateValue, 0);
                break;
            case EncodeDynamicBitrateMode.Enabled:
                key.SetValue(DynamicBitrateValue, 1);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(DynamicBitrateValue);
                }

                break;
        }
    }

    private static void WriteMobileAsw(ILinkSettingsRegistryKey key, MobileAswMode mode, bool deleteWhenDefault)
    {
        switch (mode)
        {
            case MobileAswMode.Disabled:
                key.SetValue(MobileAswValue, 0);
                break;
            case MobileAswMode.Enabled:
                key.SetValue(MobileAswValue, 1);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(MobileAswValue);
                }

                break;
        }
    }

    private static void WriteSharpening(ILinkSettingsRegistryKey key, LinkSharpeningMode sharpening, bool deleteWhenDefault)
    {
        switch (sharpening)
        {
            case LinkSharpeningMode.Disabled:
                key.SetValue(SharpeningValue, 1);
                break;
            case LinkSharpeningMode.Normal:
                key.SetValue(SharpeningValue, 2);
                break;
            case LinkSharpeningMode.Quality:
                key.SetValue(SharpeningValue, 3);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(SharpeningValue);
                }

                break;
        }
    }

    private static int ReadDword(ILinkSettingsRegistryKey key, string name)
    {
        return key.GetValue(name) switch
        {
            int value => value,
            long value => (int)value,
            _ => 0
        };
    }

    private static void WriteOrClear(ILinkSettingsRegistryKey key, string name, int value, bool deleteWhenZero, bool allowNegative = false)
    {
        if (value > 0 || allowNegative && value < 0)
        {
            key.SetValue(name, value);
            return;
        }

        if (deleteWhenZero)
        {
            key.DeleteValue(name);
        }
        else if (key.GetValue(name) is not null)
        {
            key.SetValue(name, 0);
        }
    }
}
