using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Reads and writes Quest Link / Air Link overrides in the Meta RemoteHeadset registry hive.
/// Bitrate and related values take effect after reconnecting Link or restarting OVRService.
/// </summary>
public sealed class LinkSettingsService
{
    public const string RegistryPath = @"Software\Oculus\RemoteHeadset";

    private const string BitrateValue = "BitrateMbps";
    private const string EncodeWidthValue = "EncodeResolutionWidth";
    private const string HevcValue = "HEVC";
    private const string NumSlicesValue = "numSlices";

    public LinkSettings? LastApplied { get; private set; }
    public LinkApplyResult? LastResult { get; private set; }

    public LinkSettings ReadCurrent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        if (key is null)
        {
            return new LinkSettings();
        }

        return new LinkSettings
        {
            BitrateMbps = ReadDword(key, BitrateValue),
            EncodeResolutionWidth = ReadDword(key, EncodeWidthValue),
            PreferHevc = ReadDword(key, HevcValue) == 1,
            DisableSlicedEncoding = ReadDword(key, NumSlicesValue) == 1
        };
    }

    public LinkApplyResult Apply(LinkSettings settings, bool deleteUnsetOverrides = false)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
                            ?? throw new InvalidOperationException($"Could not open HKCU\\{RegistryPath}.");

            WriteOrClear(key, BitrateValue, settings.BitrateMbps, deleteUnsetOverrides);
            WriteOrClear(key, EncodeWidthValue, settings.EncodeResolutionWidth, deleteUnsetOverrides);

            if (settings.PreferHevc)
            {
                key.SetValue(HevcValue, 1, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(HevcValue, throwOnMissingValue: false);
            }

            if (settings.DisableSlicedEncoding)
            {
                key.SetValue(NumSlicesValue, 1, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(NumSlicesValue, throwOnMissingValue: false);
            }

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
