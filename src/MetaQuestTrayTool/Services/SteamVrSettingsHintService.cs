using System.IO;
using System.Text.Json;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Read-only SteamVR Video settings hints (does not write steamvr.vrsettings).
/// </summary>
public static class SteamVrSettingsHintService
{
    public static string? DescribeHints(VrConnectionStatus? status = null)
    {
        status ??= App.Instance.LinkConnection.Probe(includeEnumHmd: false);
        var steamish = status.SessionActive
                       && status.Kind is VrConnectionKind.SteamLinkOrSteamVr
                           or VrConnectionKind.MetaAirLink
                           or VrConnectionKind.MetaWiredLink;
        if (!steamish && !IsSteamVrRunning())
        {
            return null;
        }

        var path = FindSteamVrSettingsPath();
        if (path is null)
        {
            return "SteamVR: steamvr.vrsettings not found (install/run SteamVR once). "
                   + "Use SteamVR Settings → Video for resolution / motion smoothing — this tool does not edit those.";
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            double? scale = null;
            bool? motionSmoothing = null;
            bool? advancedSupersample = null;

            if (root.TryGetProperty("steamvr", out var steamvr))
            {
                if (steamvr.TryGetProperty("supersampleScale", out var ss) && ss.TryGetDouble(out var s))
                {
                    scale = s;
                }

                if (steamvr.TryGetProperty("motionSmoothing", out var ms))
                {
                    motionSmoothing = ms.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => ms.GetInt32() != 0,
                        _ => null
                    };
                }

                if (steamvr.TryGetProperty("allowSupersampleFiltering", out var filt))
                {
                    advancedSupersample = filt.ValueKind == JsonValueKind.True
                                          || (filt.ValueKind == JsonValueKind.Number && filt.GetInt32() != 0);
                }
            }

            var parts = new List<string> { $"SteamVR Video (read-only from {Path.GetFileName(path)})" };
            if (scale is double sc)
            {
                parts.Add($"supersample ×{sc:0.00}");
            }

            if (motionSmoothing is bool smoothing)
            {
                parts.Add(smoothing ? "motion smoothing on" : "motion smoothing off");
            }

            if (advancedSupersample is bool adv)
            {
                parts.Add(adv ? "advanced SS filtering on" : "advanced SS filtering off");
            }

            parts.Add("Change these in SteamVR Settings → Video (not Meta Link registry).");
            return string.Join(" · ", parts);
        }
        catch (Exception ex)
        {
            return $"SteamVR settings unreadable: {ex.Message}";
        }
    }

    private static bool IsSteamVrRunning()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("vrserver");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static string? FindSteamVrSettingsPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = key?.GetValue("SteamPath") as string;
            if (string.IsNullOrWhiteSpace(steamPath))
            {
                steamPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam");
            }

            steamPath = steamPath.Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.Combine(steamPath, "config", "steamvr.vrsettings");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Some installs keep a copy under SteamVR app data.
            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "openvr", "steamvr.vrsettings");
            return File.Exists(local) ? local : null;
        }
        catch
        {
            return null;
        }
    }
}
