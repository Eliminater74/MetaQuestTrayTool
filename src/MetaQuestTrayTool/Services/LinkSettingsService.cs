using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Reads and writes Quest Link / Air Link overrides in the Meta RemoteHeadset registry hive.
/// Value names match OculusDebugTool.exe (see docs/ODT-REGISTRY.md).
/// </summary>
public sealed class LinkSettingsService
{
    public const string RegistryPath = @"Software\Oculus\RemoteHeadset";

    // ODT GUI registry names from OculusDebugTool.exe string table.
    private const string BitrateValue = "BitrateMbps";
    private const string EncodeWidthOdtValue = "EncodeWidth";
    private const string EncodeWidthRuntimeValue = "EncodeResolutionWidth";
    private const string HevcValue = "HEVC";
    private const string NumSlicesValue = "NumSlices";
    private const string NumSlicesAliasValue = "numSlices";
    private const string SharpeningValue = "LinkSharpeningEnabled";
    private const string DistortionValue = "DistortionCurve";
    private const string DynamicBitrateValue = "DBR";
    private const string DynamicBitrateMaxValue = "DBRMax";
    private const string DynamicBitrateOffsetValue = "DBROffsetMbps";
    private const string MobileAswValue = "MobileASWMode";

    public LinkSettings? LastApplied { get; private set; }
    public LinkApplyResult? LastResult { get; private set; }

    public LinkSettings ReadCurrent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        if (key is null)
        {
            return new LinkSettings();
        }

        var encodeWidth = ReadDword(key, EncodeWidthRuntimeValue);
        if (encodeWidth <= 0)
        {
            encodeWidth = ReadDword(key, EncodeWidthOdtValue);
        }

        return new LinkSettings
        {
            BitrateMbps = ReadDword(key, BitrateValue),
            EncodeResolutionWidth = encodeWidth,
            PreferHevc = ReadDword(key, HevcValue) == 1,
            DisableSlicedEncoding = ReadDword(key, NumSlicesValue) == 1
                                    || ReadDword(key, NumSlicesAliasValue) == 1,
            DistortionCurvature = ReadDistortion(key),
            EncodeDynamicBitrate = ReadDynamicBitrate(key),
            DynamicBitrateMax = ReadDword(key, DynamicBitrateMaxValue),
            DynamicBitrateOffsetMbps = ReadDword(key, DynamicBitrateOffsetValue),
            MobileAsw = ReadMobileAsw(key),
            Sharpening = ReadDword(key, SharpeningValue) switch
            {
                0 => LinkSharpeningMode.Disabled,
                1 => LinkSharpeningMode.Normal,
                2 or 3 => LinkSharpeningMode.Quality,
                _ => LinkSharpeningMode.Default
            }
        };
    }

    public LinkApplyResult Apply(LinkSettings settings, bool deleteUnsetOverrides = false)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
                            ?? throw new InvalidOperationException($"Could not open HKCU\\{RegistryPath}.");

            WriteOrClear(key, BitrateValue, settings.BitrateMbps, deleteUnsetOverrides);
            WriteEncodeWidth(key, settings.EncodeResolutionWidth, deleteUnsetOverrides);

            if (settings.PreferHevc)
            {
                key.SetValue(HevcValue, 1, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(HevcValue, throwOnMissingValue: false);
            }

            WriteSlicedEncoding(key, settings.DisableSlicedEncoding);
            WriteDistortion(key, settings.DistortionCurvature, deleteUnsetOverrides);
            WriteDynamicBitrate(key, settings.EncodeDynamicBitrate, deleteUnsetOverrides);
            WriteOrClear(key, DynamicBitrateMaxValue, settings.DynamicBitrateMax, deleteUnsetOverrides);
            WriteOrClear(key, DynamicBitrateOffsetValue, settings.DynamicBitrateOffsetMbps, deleteUnsetOverrides);
            WriteMobileAsw(key, settings.MobileAsw, deleteUnsetOverrides);
            WriteSharpening(key, settings.Sharpening, deleteUnsetOverrides);

            LastApplied = settings.Clone();
            var current = ReadCurrent();
            LastResult = new LinkApplyResult
            {
                Succeeded = true,
                Written = LastApplied,
                Current = current,
                Summary = $"Wrote Link settings: {settings.Describe()}. Restart Link or OVRService for full effect."
            };
            return LastResult;
        }
        catch (Exception ex)
        {
            LastResult = new LinkApplyResult
            {
                Succeeded = false,
                Current = ReadCurrent(),
                Summary = $"Could not write Link settings: {ex.Message}"
            };
            return LastResult;
        }
    }

    public string DescribeRegistryStatus()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        if (key is null)
        {
            return $"Registry hive HKCU\\{RegistryPath} was not found yet.";
        }

        return $"Live Link registry: {ReadCurrent().Describe()}";
    }

    private static DistortionCurvature ReadDistortion(RegistryKey key)
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

    private static EncodeDynamicBitrateMode ReadDynamicBitrate(RegistryKey key)
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

    private static MobileAswMode ReadMobileAsw(RegistryKey key)
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

    private static void WriteEncodeWidth(RegistryKey key, int width, bool deleteWhenZero)
    {
        if (width > 0)
        {
            key.SetValue(EncodeWidthOdtValue, width, RegistryValueKind.DWord);
            key.SetValue(EncodeWidthRuntimeValue, width, RegistryValueKind.DWord);
            return;
        }

        if (deleteWhenZero)
        {
            key.DeleteValue(EncodeWidthOdtValue, throwOnMissingValue: false);
            key.DeleteValue(EncodeWidthRuntimeValue, throwOnMissingValue: false);
        }
        else
        {
            WriteOrClear(key, EncodeWidthOdtValue, 0, deleteWhenZero: false);
            WriteOrClear(key, EncodeWidthRuntimeValue, 0, deleteWhenZero: false);
        }
    }

    private static void WriteSlicedEncoding(RegistryKey key, bool disable)
    {
        if (disable)
        {
            key.SetValue(NumSlicesValue, 1, RegistryValueKind.DWord);
            key.SetValue(NumSlicesAliasValue, 1, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(NumSlicesValue, throwOnMissingValue: false);
            key.DeleteValue(NumSlicesAliasValue, throwOnMissingValue: false);
        }
    }

    private static void WriteDistortion(RegistryKey key, DistortionCurvature curvature, bool deleteWhenDefault)
    {
        switch (curvature)
        {
            case DistortionCurvature.Low:
                key.SetValue(DistortionValue, 0, RegistryValueKind.DWord);
                break;
            case DistortionCurvature.High:
                key.SetValue(DistortionValue, 1, RegistryValueKind.DWord);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(DistortionValue, throwOnMissingValue: false);
                }

                break;
        }
    }

    private static void WriteDynamicBitrate(RegistryKey key, EncodeDynamicBitrateMode mode, bool deleteWhenDefault)
    {
        switch (mode)
        {
            case EncodeDynamicBitrateMode.Disabled:
                key.SetValue(DynamicBitrateValue, 0, RegistryValueKind.DWord);
                break;
            case EncodeDynamicBitrateMode.Enabled:
                key.SetValue(DynamicBitrateValue, 1, RegistryValueKind.DWord);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(DynamicBitrateValue, throwOnMissingValue: false);
                }

                break;
        }
    }

    private static void WriteMobileAsw(RegistryKey key, MobileAswMode mode, bool deleteWhenDefault)
    {
        switch (mode)
        {
            case MobileAswMode.Disabled:
                key.SetValue(MobileAswValue, 0, RegistryValueKind.DWord);
                break;
            case MobileAswMode.Enabled:
                key.SetValue(MobileAswValue, 1, RegistryValueKind.DWord);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(MobileAswValue, throwOnMissingValue: false);
                }

                break;
        }
    }

    private static void WriteSharpening(RegistryKey key, LinkSharpeningMode sharpening, bool deleteWhenDefault)
    {
        switch (sharpening)
        {
            case LinkSharpeningMode.Disabled:
                key.SetValue(SharpeningValue, 0, RegistryValueKind.DWord);
                break;
            case LinkSharpeningMode.Normal:
                key.SetValue(SharpeningValue, 1, RegistryValueKind.DWord);
                break;
            case LinkSharpeningMode.Quality:
                key.SetValue(SharpeningValue, 3, RegistryValueKind.DWord);
                break;
            default:
                if (deleteWhenDefault)
                {
                    key.DeleteValue(SharpeningValue, throwOnMissingValue: false);
                }

                break;
        }
    }

    private static int ReadDword(RegistryKey key, string name)
    {
        return key.GetValue(name) switch
        {
            int value => value,
            long value => (int)value,
            _ => 0
        };
    }

    private static void WriteOrClear(RegistryKey key, string name, int value, bool deleteWhenZero)
    {
        if (value > 0)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
            return;
        }

        if (deleteWhenZero)
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        else if (key.GetValue(name) is not null)
        {
            key.SetValue(name, 0, RegistryValueKind.DWord);
        }
    }
}
