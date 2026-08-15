using System.Diagnostics;
using System.Windows;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;
using MetaQuestTrayTool.Tray;
using MetaQuestTrayTool.Views.Pages;

namespace MetaQuestTrayTool;

public partial class App : System.Windows.Application
{
    public static string AppName => AppInfo.ProductName;
    private const string MutexName = @"Global\MetaQuestTrayTool.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private TrayIconHost? _tray;
    private ProcessWatcherService? _processWatcher;
    private AudioSwitchWatcher? _audioWatcher;
    private PowerWatchService? _powerWatcher;
    private HeadsetWatchService? _headsetWatcher;
    private UpdateWatchService? _updateWatcher;
    private SteamLinkAssistService? _steamLinkAssist;
    private LinkSessionWatchService? _linkSessionWatcher;
    private DashToSteamVrService? _dashToSteamVr;

    public static App Instance => (App)Current;
    public SettingsService Settings { get; } = new();
    public LogService Log { get; } = new();
    public OculusRuntimeService Oculus { get; } = new();
    public OculusDebugToolService DebugTool { get; }
    public ProfileService Profiles { get; }
    public GameLibraryService Library { get; } = new();
    public LinkSettingsService Link { get; } = new();
    public AudioDeviceService Audio { get; } = new();
    public PowerPlanService Power { get; } = new();
    public OpenXrRuntimeService OpenXr { get; } = new();
    public StartupRegistrationService StartupRegistration { get; } = new();
    public AdbService Adb { get; } = new();
    public HeadsetSettingsService Headset { get; }
    public CustomCommandService CustomCommands { get; }
    public HotKeyCommandService HotKeyCommands { get; }
    public HotKeyService HotKeys { get; }
    public VoiceCommandService Voice { get; }
    public HeadsetAnnouncerService HeadsetAnnouncer { get; }
    public UpdateService Updates { get; }
    public LinkConnectionProbeService LinkConnection { get; }
    public SteamLinkAssistService SteamLinkAssist { get; }
    public DashToSteamVrService DashToSteamVr { get; }
    public GpuDetectionService Gpu { get; } = new();
    public PcvrReadyService PcvrReady { get; }
    public SessionRecoverService SessionRecover { get; }
    public GameLaunchService GameLaunch { get; }
    public OverlayCloseService OverlayClose { get; }
    public SteamVrInstallService SteamVrInstall { get; } = new();
    public StatusDashboardService StatusDashboard { get; }

    public ProcessWatcherService? ProcessWatcher => _processWatcher;
    public PowerWatchService? PowerWatch => _powerWatcher;
    public AudioSwitchWatcher? AudioWatch => _audioWatcher;
    public HeadsetWatchService? HeadsetWatch => _headsetWatcher;
    public LinkSessionWatchService? LinkSessionWatch => _linkSessionWatcher;
    public bool IsGameProfileActive => _processWatcher?.IsProfileActive == true;
    public string? ActiveProfileName => _processWatcher?.ActiveProfileName;
    public GameProfile? ActiveProfile => _processWatcher?.GetActiveProfile();

    public App()
    {
        InitializeComponent();
        DebugTool = new OculusDebugToolService(Oculus);
        Profiles = new ProfileService(Settings);
        Headset = new HeadsetSettingsService(Adb);
        CustomCommands = new CustomCommandService(DebugTool, Adb);
        HotKeyCommands = new HotKeyCommandService(this);
        HotKeys = new HotKeyService(this, HotKeyCommands);
        Voice = new VoiceCommandService(this, HotKeyCommands);
        HeadsetAnnouncer = new HeadsetAnnouncerService(this);
        Updates = new UpdateService(this);
        LinkConnection = new LinkConnectionProbeService(this);
        SteamLinkAssist = new SteamLinkAssistService(this);
        DashToSteamVr = new DashToSteamVrService(this);
        PcvrReady = new PcvrReadyService(this);
        SessionRecover = new SessionRecoverService(this);
        GameLaunch = new GameLaunchService(this);
        OverlayClose = new OverlayCloseService(this);
        StatusDashboard = new StatusDashboardService(this);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (SessionHelperHost.IsHelperProcess(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SessionHelperHost.Attach(this);
            return;
        }

        var restarting = e.Args.Any(arg => string.Equals(arg, "--restart", StringComparison.OrdinalIgnoreCase));
        if (!TryTakeSingleInstance(restarting, out _singleInstanceMutex))
        {
            System.Windows.MessageBox.Show(
                $"{AppName} is already running in the notification area (system tray).\n\nRight-click the headset icon to open settings or Exit.",
                AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception.", args.Exception);
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        Settings.Load();
        ThemeService.Apply(Settings.Current.Tray.Theme);
        Log.Info($"{AppName} {GetVersion()} started.");
        if (Settings.UsedFallbackSettings)
        {
            Log.Warn(
                "settings.json could not be read (left on disk). Using session defaults until you Reset or Import — Save will not overwrite the corrupt file.");
        }

        if (StartupRegistration.TryEnterHandsFreeMode(
                Settings.Current,
                message => Log.Info(message),
                (message, ex) => Log.Error(message, ex)))
        {
            return;
        }

        Settings.Save();
        Log.Info(StartupRegistration.DescribeStatus());
        Adb.Refresh();
        Headset.ReadIdentity(Settings.Current.Headset);
        Settings.Save();
        Log.Info(Adb.DescribeStatus());
        Oculus.Refresh();
        Log.Info(Oculus.DescribeStatus());
        var bootPref = Oculus.EnsurePreferredBootStartMode(Settings.Current.Service.PreferManualOvrServiceAtBoot);
        if (!string.IsNullOrWhiteSpace(bootPref))
        {
            Log.Info(bootPref);
        }

        Log.Info(Gpu.Describe());
        Log.Info(DebugTool.IsAvailable
            ? $"Debug Tool CLI: {DebugTool.CliPath}"
            : "Debug Tool CLI was not found.");
        if (DebugTool.IsAvailable)
        {
            var headsets = DebugTool.EnumerateHeadsets();
            Log.Info(headsets.Count == 0
                ? "Debug Tool reported no headset serials (headset may be idle/disconnected)."
                : "Debug Tool headsets: " + string.Join(", ", headsets));
            var asw = DebugTool.QueryAswMode();
            if (!string.IsNullOrWhiteSpace(asw))
            {
                Log.Info($"Debug Tool live ASW mode: {asw}");
            }
        }

        if (Settings.Current.Service.StartServiceWhenToolStarts)
        {
            Log.Info(Oculus.Start());
        }

        if (Settings.Current.ApplyGameSettingsOnStart || Settings.Current.ApplyLinkSettingsOnStart)
        {
            Log.Info(ApplyGlobalBaseline());
        }
        else
        {
            Log.Info(Link.DescribeRegistryStatus());
        }

        Log.Info(OpenXr.Describe());

        if (Settings.Current.Power is { AutoSwitchEnabled: true, ApplyOn: PowerPlanTrigger.ToolStartExit })
        {
            Log.Info(Power.ApplyVrPlan(Settings.Current.Power));
        }

        _tray = new TrayIconHost(this);
        _tray.Show();

        _processWatcher = new ProcessWatcherService(this);
        _processWatcher.Start();

        _audioWatcher = new AudioSwitchWatcher(this);
        _audioWatcher.Start();

        _powerWatcher = new PowerWatchService(this);
        _powerWatcher.Start();

        _headsetWatcher = new HeadsetWatchService(this);
        _headsetWatcher.Start();

        HotKeys.Reload();
        Voice.Reload();
        HeadsetAnnouncer.Reload();

        _updateWatcher = new UpdateWatchService(this);
        _updateWatcher.Start();

        _steamLinkAssist = SteamLinkAssist;
        _steamLinkAssist.Start();

        _linkSessionWatcher = new LinkSessionWatchService(this);
        _linkSessionWatcher.Start();

        _dashToSteamVr = DashToSteamVr;
        _dashToSteamVr.Start();
        if (UnelevatedProcessLauncher.IsCurrentProcessElevated())
        {
            Log.Info(SessionHelperClient.EnsureRunning());
            if (SessionHelperClient.IsSteamRunningElevated())
            {
                Log.Info(
                    "Steam is running as Administrator — SteamVR / steam:// will skip the helper so they match that Steam.");
            }
        }

        if (Settings.Current.Tray.CheckForUpdatesOnStart)
        {
            _ = CheckForUpdatesOnStartAsync();
        }
    }

    private async Task CheckForUpdatesOnStartAsync()
    {
        try
        {
            await Task.Delay(2500).ConfigureAwait(false);
            await Updates.CheckInteractivelyAsync(owner: null, quietIfUpToDate: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn($"Startup update check failed: {ex.Message}");
        }
    }

    public void TrayNotify(string title, string message) => _tray?.Notify(title, message);

    public void NotifyStillRunningInTray() => _tray?.NotifyStillRunning();

    /// <summary>
    /// Re-bind timers / listeners after settings reset or import so watchers match disk state.
    /// </summary>
    public void ReloadFeatureWatchers()
    {
        HotKeys.Reload();
        Voice.Reload();
        HeadsetAnnouncer.Reload();
        ThemeService.Apply(Settings.Current.Tray.Theme);
        _audioWatcher?.SyncTimer();
        _powerWatcher?.SyncTimer();
        _headsetWatcher?.SyncWatch();
        if (Settings.Current.AutoApplyProfiles)
        {
            _processWatcher?.Start();
        }
        else
        {
            _processWatcher?.Stop();
        }

        DashToSteamVr.SyncSessionWatch();
        DashToSteamVr.SyncSteamVrExitWatch();
        Log.Info("Reloaded feature watchers after settings change.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SessionHelperClient.RequestQuit();
        // Stop session watchers first so stopping OVRService does not fire exit toasts / audio restore races.
        _dashToSteamVr?.Dispose();
        _linkSessionWatcher?.Dispose();
        _steamLinkAssist?.Dispose();
        _updateWatcher?.Dispose();
        _headsetWatcher?.Dispose();
        _powerWatcher?.Dispose();
        _audioWatcher?.Dispose();
        _processWatcher?.Dispose();
        HotKeys.Dispose();
        Voice.Dispose();
        HeadsetAnnouncer.Dispose();

        if (Settings.Current.Service.StopServiceWhenToolExits)
        {
            Log.Info(Oculus.Stop());
        }

        if (Settings.Current.Power is { AutoSwitchEnabled: true, ApplyOn: PowerPlanTrigger.ToolStartExit })
        {
            Log.Info(Power.RestoreFallbackPlan(Settings.Current.Power));
        }

        _tray?.Dispose();
        Settings.Save();
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // not owned
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    public static string GetVersion() => AppInfo.Version;

    private static bool TryTakeSingleInstance(bool restarting, out Mutex? mutex)
    {
        mutex = null;
        if (restarting)
        {
            WaitForOtherInstances(TimeSpan.FromSeconds(4));
        }
        else if (FindOtherInstance() is not null)
        {
            return false;
        }

        var attempts = restarting ? 20 : 1;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                mutex?.Dispose();
                mutex = new Mutex(true, MutexName, out var created);
                if (created)
                {
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                mutex = null;
                if (!restarting)
                {
                    return false;
                }
            }

            if (i + 1 < attempts)
            {
                Thread.Sleep(150);
            }
        }

        return restarting && FindOtherInstance() is null;
    }

    private static Process? FindOtherInstance()
    {
        var current = Process.GetCurrentProcess();
        return Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(process => process.Id != current.Id);
    }

    private static void WaitForOtherInstances(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && FindOtherInstance() is not null)
        {
            Thread.Sleep(100);
        }
    }

    public string ApplyProfile(GameProfile profile)
    {
        var caps = LinkConnection.GetCapabilities();
        var parts = new List<string>();

        if (caps.AllowsOculusDebugTool)
        {
            var result = DebugTool.Apply(profile.Settings, profile.CustomCommands.CliCommands);
            parts.Add(result.Summary);
        }
        else
        {
            parts.Add(caps.OdtSkipMessage);
            Log.Info(caps.OdtSkipMessage);
        }

        if (profile.CustomCommands.AdbCommands.Count > 0)
        {
            try
            {
                var quest = Headset.ReadIdentity(Settings.Current.Headset);
                if (quest.IsVrHeadset && quest.IsReady && !quest.IsRogue && !string.IsNullOrWhiteSpace(quest.AdbSerial))
                {
                    parts.Add(CustomCommands.ApplyAdb(profile.CustomCommands.AdbCommands, quest.AdbSerial));
                }
            }
            catch (Exception ex)
            {
                parts.Add("Custom ADB skipped: " + ex.Message);
            }
        }

        if (profile.Link.HasAny)
        {
            if (caps.AllowsMetaLinkRegistry)
            {
                var merged = profile.Link.Overlay(Settings.Current.LinkSettings);
                var link = Link.Apply(merged, deleteUnsetOverrides: true);
                parts.Add(link.Succeeded
                    ? profile.Link.Describe() + " (reconnect Link if the stream does not change)."
                    : link.Summary);
            }
            else
            {
                parts.Add(caps.MetaLinkSkipMessage);
                Log.Info(caps.MetaLinkSkipMessage);
            }
        }

        if (profile.OpenXrRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            OpenXr.CaptureBeforeProfile();
            parts.Add(OpenXr.Set(profile.OpenXrRuntime));
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    /// <summary>
    /// Copy the current live game / Link defaults into the active personal profile ("remember last good").
    /// </summary>
    public string SaveLastGoodToActiveProfile()
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return "No personal profile is active — start a game with auto-apply first.";
        }

        profile.Settings = Settings.Current.DefaultGameSettings.Clone();
        var link = Settings.Current.LinkSettings;
        if (profile.Link.HasAny
            || link.BitrateMbps > 0
            || link.EncodeResolutionWidth > 0
            || link.Sharpening != LinkSharpeningMode.Default)
        {
            profile.Link.BitrateMbps = link.BitrateMbps > 0 ? link.BitrateMbps : profile.Link.BitrateMbps;
            profile.Link.EncodeResolutionWidth = link.EncodeResolutionWidth > 0
                ? link.EncodeResolutionWidth
                : profile.Link.EncodeResolutionWidth;
            if (link.Sharpening != LinkSharpeningMode.Default)
            {
                profile.Link.Sharpening = link.Sharpening;
            }
        }

        Profiles.Save();
        Settings.Save();
        var summary =
            $"Saved last-good settings into '{profile.Name}' ({profile.Settings.Describe()}). "
            + "Next launch of this game will use them.";
        Log.Info(summary);
        TrayNotify("Profile updated", $"{profile.Name}\n{profile.Settings.Describe()}");
        return summary;
    }

    public string RestoreGlobalDefaults()
    {
        var caps = LinkConnection.GetCapabilities();
        var parts = new List<string>();

        if (caps.AllowsOculusDebugTool && DebugTool.IsAvailable)
        {
            parts.Add(ApplyGlobalGameSettings(includeOdt: true));
        }
        else
        {
            if (!caps.AllowsOculusDebugTool)
            {
                parts.Add(caps.OdtSkipMessage);
            }

            var adb = TryApplyCustomAdb(Settings.Current.CustomCommands.AdbCommands);
            if (!string.IsNullOrWhiteSpace(adb))
            {
                parts.Add(adb);
            }
        }

        if (caps.AllowsMetaLinkRegistry)
        {
            var link = Link.Apply(Settings.Current.LinkSettings, deleteUnsetOverrides: true);
            parts.Add(link.Summary);
        }
        else
        {
            parts.Add(caps.MetaLinkSkipMessage);
        }

        parts.Add(OpenXr.RestoreAfterProfile(Settings.Current.OpenXr.PreferredRuntime));

        var summary = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (summary.Length == 0)
        {
            return "Global defaults restored.";
        }

        if (caps.AllowsMetaLinkRegistry)
        {
            return $"{summary} Link: {Settings.Current.LinkSettings.Describe()}.";
        }

        return summary;
    }

    public string ApplyGlobalBaseline(bool includeLink = true, bool includeOpenXrRestore = false, bool notify = false)
    {
        if (IsGameProfileActive)
        {
            return "Global baseline skipped — a personal profile is active.";
        }

        var caps = LinkConnection.GetCapabilities();
        var parts = new List<string>();
        if (Settings.Current.ApplyGameSettingsOnStart)
        {
            if (caps.AllowsOculusDebugTool && DebugTool.IsAvailable)
            {
                parts.Add(ApplyGlobalGameSettings(includeOdt: true));
            }
            else if (!caps.AllowsOculusDebugTool)
            {
                parts.Add(caps.OdtSkipMessage);
                var adb = TryApplyCustomAdb(Settings.Current.CustomCommands.AdbCommands);
                if (!string.IsNullOrWhiteSpace(adb))
                {
                    parts.Add(adb);
                }
            }
        }

        if (includeLink && Settings.Current.ApplyLinkSettingsOnStart)
        {
            if (caps.AllowsMetaLinkRegistry)
            {
                var link = Link.Apply(Settings.Current.LinkSettings, deleteUnsetOverrides: true);
                parts.Add(link.Summary);
            }
            else
            {
                parts.Add(caps.MetaLinkSkipMessage);
            }
        }

        if (includeOpenXrRestore)
        {
            parts.Add(OpenXr.RestoreAfterProfile(Settings.Current.OpenXr.PreferredRuntime));
        }
        else if (!includeOpenXrRestore
                 && Settings.Current.OpenXr.ApplyOnStart
                 && Settings.Current.OpenXr.PreferredRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            parts.Add(OpenXr.Set(Settings.Current.OpenXr.PreferredRuntime));
        }

        var summary = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (notify && Settings.Current.ShowNotifications && summary.Length > 0)
        {
            TrayNotify("Global defaults", "Applied your global VR settings.");
        }

        return summary.Length == 0 ? "Global baseline ready (nothing to push)." : summary;
    }

    public string ApplyGlobalGameSettings(bool includeOdt = true)
    {
        var caps = LinkConnection.GetCapabilities();
        var extras = Settings.Current.CustomCommands;
        var parts = new List<string>();

        if (includeOdt)
        {
            if (!caps.AllowsOculusDebugTool)
            {
                parts.Add(caps.OdtSkipMessage);
                Log.Info(caps.OdtSkipMessage);
            }
            else
            {
                var result = DebugTool.Apply(Settings.Current.DefaultGameSettings, extras.CliCommands);
                parts.Add(result.Summary);
            }
        }

        if (extras.AdbCommands.Count > 0)
        {
            parts.Add(TryApplyCustomAdb(extras.AdbCommands));
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    /// <summary>Applies Meta Link registry only when the current session uses Meta's pipeline.</summary>
    public string ApplyMetaLinkSettings(LinkSettings settings, bool deleteUnsetOverrides = true)
    {
        var caps = LinkConnection.GetCapabilities();
        if (!caps.AllowsMetaLinkRegistry)
        {
            Log.Info($"Skipped Meta Link apply — {caps.MetaLinkSkipMessage}");
            return caps.MetaLinkSkipMessage;
        }

        var result = Link.Apply(settings, deleteUnsetOverrides);
        if (result.Succeeded)
        {
            Log.Info($"Applied Meta Link settings — {result.Summary}");
        }
        else
        {
            Log.Error($"Meta Link apply failed — {result.Summary}");
        }

        return result.Summary;
    }

    /// <summary>
    /// Detect GPU tier and apply matching Link + global game presets (no NVIDIA driver registry writes).
    /// </summary>
    public string ApplyGpuRecommendedPresets()
    {
        var recommendation = Gpu.GetRecommendation(forceRefresh: true);
        if (recommendation is null)
        {
            return "No GPU detected — could not apply recommended presets.";
        }

        Settings.Current.LinkSettings = recommendation.Link.Clone();
        Settings.Current.DefaultGameSettings = recommendation.Game.Clone();
        Settings.Save();

        var parts = new List<string>
        {
            $"GPU {recommendation.Adapter.Name} ({recommendation.Adapter.TierLabel})",
            $"Link → {recommendation.LinkPresetName}",
            $"Globals → {recommendation.GlobalPresetName}"
        };

        parts.Add(ApplyMetaLinkSettings(Settings.Current.LinkSettings, deleteUnsetOverrides: true));
        parts.Add(ApplyGlobalGameSettings(includeOdt: true));
        parts.Add(recommendation.Rationale);

        var summary = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        Log.Info("Applied GPU recommended presets — " + summary);
        return summary;
    }

    private string TryApplyCustomAdb(IReadOnlyList<string> commands)
    {
        try
        {
            var quest = Headset.ReadIdentity(Settings.Current.Headset);
            if (!quest.IsVrHeadset || !quest.IsReady || quest.IsRogue || string.IsNullOrWhiteSpace(quest.AdbSerial))
            {
                return quest.IsIgnored
                    ? "Custom ADB skipped (not a VR headset)."
                    : quest.IsRogue
                        ? "Custom ADB skipped (untrusted headset)."
                        : "Custom ADB skipped (no VR headset).";
            }

            return CustomCommands.ApplyAdb(commands, quest.AdbSerial);
        }
        catch (Exception ex)
        {
            return "Custom ADB skipped: " + ex.Message;
        }
    }
}
