using System.IO;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches the Windows OpenXR active runtime between Meta / Oculus and SteamVR.
/// Writes HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime through explicit 64-bit and 32-bit registry views.
/// </summary>
public sealed class OpenXrRuntimeService
{
    public const string RegistryPath = @"SOFTWARE\Khronos\OpenXR\1";
    public const string WowRegistryPath = @"SOFTWARE\WOW6432Node\Khronos\OpenXR\1";
    public const string ValueName = "ActiveRuntime";

    private string? _capturedBeforeProfile;

    public OpenXrRuntimeKind? ReadActiveKind() => ReadActiveKind(RegistryView.Registry64);

    internal OpenXrRuntimeKind? ReadActiveKind(RegistryView view)
    {
        var path = ReadActivePath(view);
        return path is null ? null : Classify(path);
    }

    public string? ReadActivePath() => ReadActivePath(RegistryView.Registry64);

    internal string? ReadActivePath(RegistryView view)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(RegistryPath, writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch
        {
            return null;
        }
    }

    public string Describe() => DescribeViews(ReadRuntimeView(RegistryView.Registry64), ReadRuntimeView(RegistryView.Registry32));

    internal OpenXrRuntimeViewState ReadRuntimeView(RegistryView view)
    {
        var path = ReadActivePath(view);
        return new OpenXrRuntimeViewState(ViewLabel(view), path is null ? null : Classify(path), path);
    }

    internal static string DescribeViews(OpenXrRuntimeViewState x64, OpenXrRuntimeViewState x86)
    {
        if (string.IsNullOrWhiteSpace(x64.Path) && string.IsNullOrWhiteSpace(x86.Path))
        {
            return "OpenXR: no ActiveRuntime is set.";
        }

        if (!string.IsNullOrWhiteSpace(x64.Path)
            && string.Equals(x64.Path, x86.Path, StringComparison.OrdinalIgnoreCase))
        {
            return $"OpenXR: {Label(x64.Kind)} ({x64.Path})";
        }

        return $"OpenXR: {DescribeView(x64)}; {DescribeView(x86)}";
    }

    public bool IsAvailable(OpenXrRuntimeKind kind) => !string.IsNullOrWhiteSpace(ResolveJson(kind, RegistryView.Registry64));

    public string Set(OpenXrRuntimeKind kind) => SetResult(kind).Summary;

    public ProfileStepResult SetResult(OpenXrRuntimeKind kind)
    {
        if (kind is OpenXrRuntimeKind.Inherit)
        {
            return new("OpenXR", ProfileStepStatus.Skipped, "OpenXR inherit - no registry change.");
        }

        var json64 = ResolveJson(kind, RegistryView.Registry64);
        if (string.IsNullOrWhiteSpace(json64) || !File.Exists(json64))
        {
            return new("OpenXR", ProfileStepStatus.Failed, kind == OpenXrRuntimeKind.SteamVr
                ? "SteamVR OpenXR JSON was not found. Install SteamVR."
                : "Meta OpenXR JSON was not found. Install the Meta Quest / Oculus PC software.");
        }

        var json32 = ResolveJson(kind, RegistryView.Registry32);
        if (string.IsNullOrWhiteSpace(json32) || !File.Exists(json32))
        {
            json32 = null;
        }

        var write = WriteActiveRuntimes(json64, json32);
        var active64 = ReadRuntimeView(RegistryView.Registry64);
        var active32 = ReadRuntimeView(RegistryView.Registry32);
        var view32Matches = json32 is null
            ? string.IsNullOrWhiteSpace(active32.Path)
            : active32.Kind == kind;
        if (active64.Kind == kind && view32Matches && write.Succeeded)
        {
            return new(
                "OpenXR",
                ProfileStepStatus.Succeeded,
                $"OpenXR runtime set to {Label(kind)}. Restart the game / SteamVR / Link session to pick it up. {write.Summary}");
        }

        return new(
            "OpenXR",
            ProfileStepStatus.Failed,
            $"Tried to set OpenXR to {Label(kind)}. Live values: {DescribeView(active64)}; {DescribeView(active32)}. {write.Summary}");
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

    private string? ResolveJson(OpenXrRuntimeKind kind, RegistryView view)
    {
        foreach (var candidate in EnumerateCandidates(kind, view))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<string> EnumerateCandidates(OpenXrRuntimeKind kind, RegistryView view)
    {
        foreach (var registered in ReadAvailableRuntimes(view))
        {
            var classified = Classify(registered);
            if (classified != kind)
            {
                continue;
            }

            if (view == RegistryView.Registry64 && IsLikely32BitRuntimeJson(registered))
            {
                continue;
            }

            if (view == RegistryView.Registry32 && IsLikely64BitRuntimeJson(registered))
            {
                continue;
            }

            yield return registered;
        }

        if (kind == OpenXrRuntimeKind.Meta)
        {
            yield return view == RegistryView.Registry64
                ? @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json"
                : @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_32.json";
        }
        else if (kind == OpenXrRuntimeKind.SteamVr && view == RegistryView.Registry64)
        {
            var steam = new SteamLibraryService().DetectSteamRoot();
            if (!string.IsNullOrWhiteSpace(steam))
            {
                yield return Path.Combine(steam, "steamapps", "common", "SteamVR", "steamxr_win64.json");
            }

            yield return @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json";
        }
    }

    private static IReadOnlyList<string> ReadAvailableRuntimes(RegistryView view)
    {
        var runtimes = new List<string>();
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(RegistryPath + @"\AvailableRuntimes", writable: false);
            if (key is null)
            {
                return runtimes;
            }

            foreach (var name in key.GetValueNames())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    runtimes.Add(name);
                }
            }
        }
        catch
        {
            // Best-effort diagnostics. SetResult still reports write/read-back failures.
        }

        return runtimes;
    }

    private static (bool Succeeded, string Summary) WriteActiveRuntimes(string json64, string? json32)
    {
        try
        {
            WriteKey(RegistryView.Registry64, json64);
            if (!string.IsNullOrWhiteSpace(json32))
            {
                WriteKey(RegistryView.Registry32, json32);
                return (true, "Wrote 64-bit and 32-bit HKLM OpenXR ActiveRuntime values.");
            }

            ClearActiveRuntime(RegistryView.Registry32);
            return (true, "Wrote 64-bit HKLM OpenXR ActiveRuntime and cleared stale 32-bit ActiveRuntime because no matching 32-bit runtime JSON was found.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (false, "Cannot change OpenXR without Administrator rights. The tray must already be elevated "
                   + "(automatic at logon) — Windows will not show a UAC prompt while the headset is on.");
        }
        catch (Exception ex)
        {
            return (false, $"Could not write OpenXR ActiveRuntime: {ex.Message}");
        }
    }

    private static void WriteKey(RegistryView view, string jsonPath)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = root.OpenSubKey(RegistryPath, writable: true)
                        ?? root.CreateSubKey(RegistryPath, writable: true)
                        ?? throw new UnauthorizedAccessException($"Could not open HKLM\\{RegistryPath} in {ViewLabel(view)} view.");
        key.SetValue(ValueName, jsonPath, RegistryValueKind.String);
    }

    private static void ClearActiveRuntime(RegistryView view)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = root.OpenSubKey(RegistryPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string DescribeView(OpenXrRuntimeViewState view) => string.IsNullOrWhiteSpace(view.Path)
        ? $"{view.Name}: not set"
        : $"{view.Name}: {Label(view.Kind)} ({view.Path})";

    private static string ViewLabel(RegistryView view) => view == RegistryView.Registry32 ? "32-bit" : "64-bit";

    private static bool IsLikely32BitRuntimeJson(string path) =>
        path.Contains("openxr_32", StringComparison.OrdinalIgnoreCase)
        || path.Contains("win32", StringComparison.OrdinalIgnoreCase);

    private static bool IsLikely64BitRuntimeJson(string path) =>
        path.Contains("openxr_64", StringComparison.OrdinalIgnoreCase)
        || path.Contains("win64", StringComparison.OrdinalIgnoreCase);
}

internal readonly record struct OpenXrRuntimeViewState(
    string Name,
    OpenXrRuntimeKind? Kind,
    string? Path);
