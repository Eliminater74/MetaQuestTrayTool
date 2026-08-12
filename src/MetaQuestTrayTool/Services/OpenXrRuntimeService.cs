using System.IO;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches the Windows OpenXR active runtime between Meta / Oculus and SteamVR.
/// Writes HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime (and the 32-bit WOW6432Node key when a JSON exists).
/// </summary>
public sealed class OpenXrRuntimeService
{
    public const string RegistryPath = @"SOFTWARE\Khronos\OpenXR\1";
    public const string WowRegistryPath = @"SOFTWARE\WOW6432Node\Khronos\OpenXR\1";
    public const string ValueName = "ActiveRuntime";

    private string? _capturedBeforeProfile;

    public OpenXrRuntimeKind? ReadActiveKind()
    {
        var path = ReadActivePath();
        return path is null ? null : Classify(path);
    }

    public string? ReadActivePath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, writable: false);
        return key?.GetValue(ValueName) as string;
    }

    public string Describe()
    {
        var path = ReadActivePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return "OpenXR: no ActiveRuntime is set.";
        }

        var kind = Classify(path);
        var label = kind switch
        {
            OpenXrRuntimeKind.Meta => "Meta / Oculus",
            OpenXrRuntimeKind.SteamVr => "SteamVR",
            _ => "Unknown"
        };
        return $"OpenXR: {label} ({path})";
    }

    public bool IsAvailable(OpenXrRuntimeKind kind) => !string.IsNullOrWhiteSpace(ResolveJson(kind, prefer64: true));

    public string Set(OpenXrRuntimeKind kind)
    {
        if (kind is OpenXrRuntimeKind.Inherit)
        {
            return "OpenXR inherit — no registry change.";
        }

        var json64 = ResolveJson(kind, prefer64: true);
        if (string.IsNullOrWhiteSpace(json64) || !File.Exists(json64))
        {
            return kind == OpenXrRuntimeKind.SteamVr
                ? "SteamVR OpenXR JSON was not found. Install SteamVR."
                : "Meta OpenXR JSON was not found. Install the Meta Quest / Oculus PC software.";
        }

        var json32 = ResolveJson(kind, prefer64: false);
        if (string.IsNullOrWhiteSpace(json32) || !File.Exists(json32))
        {
            json32 = null;
        }

        var write = WriteActiveRuntimes(json64, json32);
        var current = ReadActiveKind();
        if (current == kind)
        {
            return $"OpenXR runtime set to {Label(kind)}. Restart the game / SteamVR / Link session to pick it up. {write}";
        }

        return $"Tried to set OpenXR to {Label(kind)}. Live value is still {Label(current)}. {write}";
    }

    public void CaptureBeforeProfile()
    {
        _capturedBeforeProfile ??= ReadActivePath();
    }

    public string RestoreAfterProfile(OpenXrRuntimeKind globalPreferred)
    {
        if (!string.IsNullOrWhiteSpace(_capturedBeforeProfile))
        {
            var captured = Classify(_capturedBeforeProfile);
            _capturedBeforeProfile = null;
            if (captured is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
            {
                return Set(captured);
            }
        }

        if (globalPreferred is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            return Set(globalPreferred);
        }

        return "OpenXR left unchanged.";
    }

    public static string Label(OpenXrRuntimeKind? kind) => kind switch
    {
        OpenXrRuntimeKind.Meta => "Meta / Oculus",
        OpenXrRuntimeKind.SteamVr => "SteamVR",
        OpenXrRuntimeKind.Inherit => "Inherit",
        _ => "Unknown"
    };

    private static OpenXrRuntimeKind Classify(string path)
    {
        if (path.Contains("steamxr", StringComparison.OrdinalIgnoreCase)
            || path.Contains("SteamVR", StringComparison.OrdinalIgnoreCase))
        {
            return OpenXrRuntimeKind.SteamVr;
        }

        if (path.Contains("oculus_openxr", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Oculus\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("Meta", StringComparison.OrdinalIgnoreCase))
        {
            return OpenXrRuntimeKind.Meta;
        }

        return OpenXrRuntimeKind.Inherit;
    }

    private string? ResolveJson(OpenXrRuntimeKind kind, bool prefer64)
    {
        foreach (var candidate in EnumerateCandidates(kind, prefer64))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<string> EnumerateCandidates(OpenXrRuntimeKind kind, bool prefer64)
    {
        foreach (var registered in ReadAvailableRuntimes())
        {
            var classified = Classify(registered);
            if (classified != kind)
            {
                continue;
            }

            if (prefer64 && registered.Contains("32", StringComparison.OrdinalIgnoreCase)
                && registered.Contains("openxr_32", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!prefer64 && registered.Contains("openxr_64", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return registered;
        }

        if (kind == OpenXrRuntimeKind.Meta)
        {
            yield return prefer64
                ? @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json"
                : @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_32.json";
        }
        else if (kind == OpenXrRuntimeKind.SteamVr && prefer64)
        {
            var steam = new SteamLibraryService().DetectSteamRoot();
            if (!string.IsNullOrWhiteSpace(steam))
            {
                yield return Path.Combine(steam, "steamapps", "common", "SteamVR", "steamxr_win64.json");
            }

            yield return @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json";
        }
    }

    private static IEnumerable<string> ReadAvailableRuntimes()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath + @"\AvailableRuntimes", writable: false);
        if (key is null)
        {
            yield break;
        }

        foreach (var name in key.GetValueNames())
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }

    private static string WriteActiveRuntimes(string json64, string? json32)
    {
        try
        {
            WriteKey(RegistryPath, json64);
            if (!string.IsNullOrWhiteSpace(json32))
            {
                WriteKey(WowRegistryPath, json32);
            }

            return "Wrote HKLM OpenXR ActiveRuntime.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "Cannot change OpenXR without Administrator rights. The tray must already be elevated "
                   + "(automatic at logon) — Windows will not show a UAC prompt while the headset is on.";
        }
        catch (Exception ex)
        {
            return $"Could not write OpenXR ActiveRuntime: {ex.Message}";
        }
    }

    private static void WriteKey(string keyPath, string jsonPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true)
                        ?? Registry.LocalMachine.CreateSubKey(keyPath, writable: true)
                        ?? throw new UnauthorizedAccessException($"Could not open HKLM\\{keyPath}.");
        key.SetValue(ValueName, jsonPath, RegistryValueKind.String);
    }

}
