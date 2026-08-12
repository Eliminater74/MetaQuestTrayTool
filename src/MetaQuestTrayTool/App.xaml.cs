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

    public ProcessWatcherService? ProcessWatcher => _processWatcher;
    public bool IsGameProfileActive => _processWatcher?.IsProfileActive == true;
    public string? ActiveProfileName => _processWatcher?.ActiveProfileName;

    public App()
    {
        InitializeComponent();
        DebugTool = new OculusDebugToolService(Oculus);
        Profiles = new ProfileService(Settings);
        Headset = new HeadsetSettingsService(Adb);
        CustomCommands = new CustomCommandService(DebugTool, Adb);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            if (Settings.Current.Service.LaunchOculusHomeOnServiceStart)
            {
                ServiceStartupPage.TryLaunchHome();
            }
        }

        if (Settings.Current.Service.LaunchOculusHomeOnToolStart)
        {
            ServiceStartupPage.TryLaunchHome();
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
    }

    public void TrayNotify(string title, string message) => _tray?.Notify(title, message);

    public void NotifyStillRunningInTray() => _tray?.NotifyStillRunning();

    protected override void OnExit(ExitEventArgs e)
    {
        if (Settings.Current.Service.CloseOculusHomeOnToolExit)
        {
            ServiceStartupPage.TryCloseHome();
        }

        if (Settings.Current.Service.StopServiceWhenToolExits)
        {
            Log.Info(Oculus.Stop());
        }

        if (Settings.Current.Power is { AutoSwitchEnabled: true, ApplyOn: PowerPlanTrigger.ToolStartExit })
        {
            Log.Info(Power.RestoreFallbackPlan(Settings.Current.Power));
        }

        _headsetWatcher?.Dispose();
        _powerWatcher?.Dispose();
        _audioWatcher?.Dispose();
        _processWatcher?.Dispose();
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
        var result = DebugTool.Apply(profile.Settings, profile.CustomCommands.CliCommands);
        var summary = result.Summary;
        if (profile.CustomCommands.AdbCommands.Count > 0)
        {
            try
            {
                var quest = Headset.ReadIdentity(Settings.Current.Headset);
                if (quest.IsVrHeadset && quest.IsReady && !quest.IsRogue && !string.IsNullOrWhiteSpace(quest.AdbSerial))
                {
                    summary += " " + CustomCommands.ApplyAdb(profile.CustomCommands.AdbCommands, quest.AdbSerial);
                }
            }
            catch (Exception ex)
            {
                summary += " Custom ADB skipped: " + ex.Message;
            }
        }
        if (profile.Link.HasAny)
        {
            var merged = profile.Link.Overlay(Settings.Current.LinkSettings);
            var link = Link.Apply(merged, deleteUnsetOverrides: true);
            summary += " " + (link.Succeeded
                ? profile.Link.Describe() + " (reconnect Link if the stream does not change)."
                : link.Summary);
        }

        if (profile.OpenXrRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            OpenXr.CaptureBeforeProfile();
            summary += " " + OpenXr.Set(profile.OpenXrRuntime);
        }

        return summary;
    }

    public string RestoreGlobalDefaults()
    {
        var summary = ApplyGlobalBaseline(includeLink: true, includeOpenXrRestore: true);
        return $"{summary} Restored global Link settings ({Settings.Current.LinkSettings.Describe()}).";
    }

    public string ApplyGlobalBaseline(bool includeLink = true, bool includeOpenXrRestore = false, bool notify = false)
    {
        if (IsGameProfileActive)
        {
            return "Global baseline skipped — a personal profile is active.";
        }

        var parts = new List<string>();
        if (DebugTool.IsAvailable && Settings.Current.ApplyGameSettingsOnStart)
        {
            parts.Add(ApplyGlobalGameSettings());
        }

        if (includeLink && Settings.Current.ApplyLinkSettingsOnStart)
        {
            var link = Link.Apply(Settings.Current.LinkSettings, deleteUnsetOverrides: true);
            parts.Add(link.Summary);
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

    public string ApplyGlobalGameSettings()
    {
        var extras = Settings.Current.CustomCommands;
        var result = DebugTool.Apply(Settings.Current.DefaultGameSettings, extras.CliCommands);
        var summary = result.Summary;
        if (extras.AdbCommands.Count > 0)
        {
            summary += " " + TryApplyCustomAdb(extras.AdbCommands);
        }

        return summary.Trim();
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
