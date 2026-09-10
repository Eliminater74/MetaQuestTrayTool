using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed record SupportBundleResult(
    string FilePath,
    long Bytes,
    IReadOnlyList<string> Entries)
{
    public string Summary => $"Created support ZIP ({FormatBytes(Bytes)}) at {FilePath}. Entries: {string.Join(", ", Entries)}.";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / 1024d / 1024d:0.0} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0.0} KB";
        }

        return bytes + " bytes";
    }
}

public sealed class SupportBundleService
{
    private static readonly Regex Ipv4 = new(
        @"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?::\d{1,5})?\b",
        RegexOptions.Compiled);

    private static readonly Regex LongDeviceToken = new(
        @"\b(?=[A-Z0-9_-]{8,}\b)(?=[A-Z0-9_-]*[A-Z])(?=[A-Z0-9_-]*\d)[A-Z0-9_-]+\b",
        RegexOptions.Compiled);

    private readonly App _app;

    public SupportBundleService(App app) => _app = app;

    public SupportBundleResult Create(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Support bundle output path is required.", nameof(outputPath));
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        var tempPath = fullPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        var entries = new Dictionary<string, string>
        {
            ["summary.txt"] = BuildSummary(),
            ["settings-summary.txt"] = BuildSettingsSummary(_app.Settings.Current),
            ["meta-compatibility.txt"] = _app.RuntimeCompatibility.Check(remember: false, runDebugToolProbe: false).ToDisplayText(),
            ["recent-log.txt"] = BuildRecentLog()
        };

        try
        {
            using (var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var (name, text) in entries)
                {
                    AddText(zip, name, SanitizeForSupport(text));
                }
            }

            File.Move(tempPath, fullPath, overwrite: true);
            var file = new FileInfo(fullPath);
            return new SupportBundleResult(fullPath, file.Length, entries.Keys.ToList());
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    internal static string BuildSettingsSummary(AppSettings settings)
    {
        var text = new StringBuilder();
        text.AppendLine("Settings Summary");
        text.AppendLine($"Start with Windows: {settings.StartWithWindows}");
        text.AppendLine($"Automatic elevation: {settings.AutomaticElevation}");
        text.AppendLine($"Notifications: {settings.ShowNotifications}");
        text.AppendLine($"Apply game settings on start: {settings.ApplyGameSettingsOnStart}");
        text.AppendLine($"Auto apply profiles: {settings.AutoApplyProfiles}");
        text.AppendLine($"Profiles count: {settings.Profiles.Count}");
        text.AppendLine($"Global CLI custom command count: {settings.CustomCommands.CliCommands.Count}");
        text.AppendLine($"Global ADB custom command count: {settings.CustomCommands.AdbCommands.Count}");
        text.AppendLine($"Headset ADB custom command count: {settings.Headset.CustomAdbCommands.Count}");
        text.AppendLine($"Headset apply on connect: {settings.Headset.ApplyWhenHeadsetConnects}");
        text.AppendLine($"Headset require trusted device: {settings.Headset.RequireTrustedHeadset}");
        text.AppendLine($"Headset trust configured: {!string.IsNullOrWhiteSpace(settings.Headset.TrustedSerial)}");
        text.AppendLine($"Wireless ADB configured: {!string.IsNullOrWhiteSpace(settings.Headset.WirelessHost)}");
        text.AppendLine($"Wireless auto reconnect: {settings.Headset.WirelessAutoReconnect}");
        text.AppendLine($"Headset-only wireless ADB: {settings.Headset.HeadsetOnlyWirelessAdb}");
        text.AppendLine($"Link globals: {settings.LinkSettings.Describe()}");
        text.AppendLine($"Game globals: {settings.DefaultGameSettings.Describe()}");
        text.AppendLine($"OpenXR preferred runtime: {settings.OpenXr.PreferredRuntime}");
        text.AppendLine($"OpenXR apply on start: {settings.OpenXr.ApplyOnStart}");
        text.AppendLine($"Audio auto switch: {settings.Audio.AutoSwitchEnabled}");
        text.AppendLine($"Power auto switch: {settings.Power.AutoSwitchEnabled}");
        text.AppendLine($"Hotkeys enabled: {settings.HotKeys.Enabled}");
        text.AppendLine($"Voice enabled: {settings.Voice.Enabled}");
        return text.ToString().TrimEnd();
    }

    internal static string SanitizeForSupport(string text)
    {
        var sanitized = LogService.RedactSensitiveData(text ?? string.Empty);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            sanitized = sanitized.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            sanitized = sanitized.Replace(userProfile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Ipv4.Replace(sanitized, "<ip>");
        sanitized = LongDeviceToken.Replace(sanitized, "<device-id>");
        return sanitized;
    }

    private string BuildSummary()
    {
        RuntimeSnapshot? snapshot = null;
        try
        {
            snapshot = _app.RuntimeSnapshots.Capture(includeHeadset: true, force: true);
        }
        catch
        {
            // Summary still includes app and service basics.
        }

        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        text.AppendLine($"Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine($"Elevated: {_app.StartupRegistration.IsProcessElevated}");
        text.AppendLine($"App data: {AppPaths.AppDataDirectory}");
        text.AppendLine();

        _app.Oculus.Refresh();
        text.AppendLine("Meta runtime");
        text.AppendLine($"  Installed: {_app.Oculus.IsInstalled}");
        text.AppendLine($"  Install path: {_app.Oculus.InstallPath ?? "not found"}");
        text.AppendLine($"  Service exists: {_app.Oculus.ServiceExists}");
        text.AppendLine($"  Service status: {_app.Oculus.ServiceStatus}");
        text.AppendLine($"  Runtime server: {_app.Oculus.RuntimeServerPath ?? "not found"}");
        text.AppendLine($"  Debug Tool CLI: {_app.Oculus.DebugToolCliPath ?? "not found"}");
        text.AppendLine();

        if (snapshot is not null)
        {
            text.AppendLine("Runtime snapshot");
            text.AppendLine($"  Link kind: {snapshot.Link.Kind}");
            text.AppendLine($"  Link active: {snapshot.Link.SessionActive}");
            text.AppendLine($"  Link detail: {snapshot.Link.Detail ?? "none"}");
            text.AppendLine($"  OpenXR: {OpenXrRuntimeService.Label(snapshot.OpenXr)}");
            text.AppendLine($"  OpenXR path: {snapshot.OpenXrPath ?? "none"}");
            text.AppendLine($"  SteamVR installed: {snapshot.SteamVr.IsInstalled}");
            text.AppendLine($"  SteamVR running: {snapshot.SteamVr.IsRunning}");
            text.AppendLine($"  Virtual Desktop running: {snapshot.Link.VirtualDesktopRunning}");
            text.AppendLine($"  GPU: {snapshot.Gpu?.Adapter.Summary ?? "not detected"}");
            text.AppendLine($"  Active power plan: {snapshot.ActivePowerPlan?.ToString() ?? "not read"}");
            if (snapshot.Headset is { } headset)
            {
                text.AppendLine("  ADB headset");
                text.AppendLine($"    Model: {headset.Model ?? "none"}");
                text.AppendLine($"    Device: {headset.Device ?? "none"}");
                text.AppendLine($"    Manufacturer: {headset.Manufacturer ?? "none"}");
                text.AppendLine($"    Android: {headset.AndroidVersion ?? "none"}");
                text.AppendLine($"    State: {headset.State ?? "none"}");
                text.AppendLine($"    Ready: {headset.IsReady}");
                text.AppendLine($"    VR headset: {headset.IsVrHeadset}");
                text.AppendLine($"    Trusted: {headset.IsTrusted}");
                text.AppendLine($"    Blocked: {headset.IsRogue}");
                text.AppendLine($"    Runtime: {headset.Runtime?.Summary ?? "not read"}");
            }
        }

        return text.ToString().TrimEnd();
    }

    private string BuildRecentLog()
    {
        var entries = SnapshotLogEntries()
            .TakeLast(200)
            .Select(entry => entry.ToString());
        var text = string.Join(Environment.NewLine, entries);
        return string.IsNullOrWhiteSpace(text) ? "No in-memory log entries." : text;
    }

    private IReadOnlyList<LogEntry> SnapshotLogEntries()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => _app.Log.Entries.ToList());
        }

        return _app.Log.Entries.ToList();
    }

    private static void AddText(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(text);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
