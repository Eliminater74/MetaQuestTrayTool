using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Detects SteamVR install path, file version, and Stable vs Beta from Steam's appmanifest
/// (same approach as Oculus VR Dash Manager: appmanifest_250820.acf + betakey).
/// </summary>
public sealed class SteamVrInstallService
{
    public const string SteamVrAppId = "250820";
    public const string SteamInstallUri = "steam://install/250820";
    public const string StoreUrl = "https://store.steampowered.com/app/250820/SteamVR/";

    private static readonly Regex PathRegex = new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BuildIdRegex = new("\"buildid\"\\s*\"(\\d+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirRegex = new("\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BetaKeyRegex = new(
        "\"(?:BetaKey|betakey)\"\\s*\"([^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly object _cacheLock = new();
    private SteamVrInstallInfo? _cache;
    private DateTime _cacheUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);

    public SteamVrInstallInfo Probe(bool force = false)
    {
        if (!force)
        {
            lock (_cacheLock)
            {
                if (_cache is not null && DateTime.UtcNow - _cacheUtc < CacheTtl)
                {
                    return _cache with { IsRunning = IsSteamVrRunning() };
                }
            }
        }

        var info = ProbeCore();
        lock (_cacheLock)
        {
            _cache = info;
            _cacheUtc = DateTime.UtcNow;
        }

        return info;
    }

    public string OpenInstallPage()
    {
        if (SessionHelperClient.TryLaunchUri(SteamInstallUri, out _))
        {
            return "Opened Steam to install SteamVR (app 250820). If Steam did not open, use the Store link from VR Tools.";
        }

        try
        {
            UrlLaunchService.Open(StoreUrl);
            return "Opened the SteamVR Store page in your browser.";
        }
        catch (Exception ex)
        {
            return "Could not open SteamVR install: " + ex.Message;
        }
    }

    /// <summary>
    /// Launch SteamVR Home (steamtours.exe). Meta's old Oculus Home is gone; this is the SteamVR environment.
    /// Starts SteamVR via steam://run/250820 first if it is not already running.
    /// </summary>
    public string OpenSteamVrHome()
    {
        var info = Probe(force: true);
        if (!info.IsInstalled || string.IsNullOrWhiteSpace(info.InstallPath))
        {
            return "SteamVR is not installed. Use Install SteamVR from Status / Info, then try again.";
        }

        var tours = Path.Combine(
            info.InstallPath,
            "tools",
            "steamvr_environments",
            "game",
            "bin",
            "win64",
            "steamtours.exe");

        if (!File.Exists(tours))
        {
            return "SteamVR Home (steamtours.exe) was not found under this SteamVR install. "
                   + "SteamVR Home may be disabled or removed — check SteamVR Settings → Startup / Desktop.";
        }

        if (!info.IsRunning)
        {
            if (!SessionHelperClient.TryLaunchSteamVr(out var steamVr))
            {
                return $"Could not start SteamVR before Home: {steamVr}";
            }

            Thread.Sleep(2500);
        }

        if (SessionHelperClient.TryLaunchExe(tours, arguments: null, Path.GetDirectoryName(tours), out var toursDetail))
        {
            return "Launched SteamVR Home (steamtours). Put the headset on if it does not appear.";
        }

        return $"Could not launch SteamVR Home: {toursDetail}";
    }

    private SteamVrInstallInfo ProbeCore()
    {
        var steamRoot = DetectSteamRoot();
        var running = IsSteamVrRunning();
        if (steamRoot is null)
        {
            // Still may have openvrpaths runtime without Steam registry.
            var fromOpenVr = TryFromOpenVrPaths(running);
            return fromOpenVr ?? SteamVrInstallInfo.Missing();
        }

        foreach (var library in EnumerateLibraries(steamRoot))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{SteamVrAppId}.acf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(manifest);
                var installDir = InstallDirRegex.Match(content);
                var dirName = installDir.Success ? installDir.Groups[1].Value : "SteamVR";
                var installPath = Path.Combine(library, "steamapps", "common", dirName);
                if (!Directory.Exists(installPath))
                {
                    // Manifest exists but files missing (uninstalling / incomplete).
                    continue;
                }

                var betaMatch = BetaKeyRegex.Match(content);
                var betaKey = betaMatch.Success ? betaMatch.Groups[1].Value.Trim() : null;
                var isBeta = !string.IsNullOrWhiteSpace(betaKey);
                var buildId = BuildIdRegex.Match(content) is { Success: true } build
                    ? build.Groups[1].Value
                    : null;
                var version = TryReadFileVersion(installPath);

                return new SteamVrInstallInfo
                {
                    IsInstalled = true,
                    Channel = isBeta ? SteamVrChannel.Beta : SteamVrChannel.Stable,
                    BetaKey = isBeta ? betaKey : null,
                    BuildId = buildId,
                    Version = version,
                    InstallPath = installPath,
                    ManifestPath = manifest,
                    SteamRoot = steamRoot,
                    IsRunning = running
                };
            }
            catch
            {
                // try next library
            }
        }

        return TryFromOpenVrPaths(running) ?? SteamVrInstallInfo.Missing(steamRoot);
    }

    private static SteamVrInstallInfo? TryFromOpenVrPaths(bool running)
    {
        try
        {
            var openVrPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "openvr",
                "openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                return null;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(openVrPath));
            if (!doc.RootElement.TryGetProperty("runtime", out var runtime)
                || runtime.ValueKind != System.Text.Json.JsonValueKind.Array
                || runtime.GetArrayLength() == 0)
            {
                return null;
            }

            var root = runtime[0].GetString();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return null;
            }

            // openvrpaths found a runtime but Steam manifest missing — treat as installed, channel unknown.
            return new SteamVrInstallInfo
            {
                IsInstalled = true,
                Channel = SteamVrChannel.Unknown,
                Version = TryReadFileVersion(root),
                InstallPath = root,
                IsRunning = running
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadFileVersion(string steamVrRoot)
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("bin", "win64", "vrserver.exe"),
                     Path.Combine("bin", "win64", "vrmonitor.exe"),
                     Path.Combine("bin", "win32", "vrserver.exe")
                 })
        {
            var path = Path.Combine(steamVrRoot, relative);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                {
                    return info.FileVersion;
                }

                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                {
                    return info.ProductVersion;
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    private static string? DetectSteamRoot()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
            {
                steamPath = steamPath.Replace('/', Path.DirectorySeparatorChar);
                if (Directory.Exists(steamPath))
                {
                    return steamPath;
                }
            }
        }
        catch
        {
            // fall through
        }

        foreach (var path in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                 })
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateLibraries(string steamRoot)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        var libraryFolders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFolders))
        {
            return libraries;
        }

        try
        {
            foreach (Match match in PathRegex.Matches(File.ReadAllText(libraryFolders)))
            {
                var folder = match.Groups[1].Value.Replace(@"\\", @"\").Replace('/', Path.DirectorySeparatorChar);
                if (Directory.Exists(folder))
                {
                    libraries.Add(folder);
                }
            }
        }
        catch
        {
            // return what we have
        }

        return libraries;
    }

    private static bool IsSteamVrRunning()
    {
        try
        {
            // Match Dash→SteamVR: lone vrserver without compositor is not a healthy session.
            return IsProcessRunning("vrstartup")
                   || (IsProcessRunning("vrserver")
                       && (IsProcessRunning("vrcompositor")
                           || IsProcessRunning("vrdashboard")));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessRunning(string name)
    {
        var processes = Process.GetProcessesByName(name);
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
}
