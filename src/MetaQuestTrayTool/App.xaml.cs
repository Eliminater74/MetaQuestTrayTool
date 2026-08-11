using System.Windows;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;
using MetaQuestTrayTool.Tray;
using MetaQuestTrayTool.Views.Pages;

namespace MetaQuestTrayTool;

public partial class App : System.Windows.Application
{
    public const string AppName = "Meta Quest Tray Tool";
    private const string MutexName = @"Local\MetaQuestTrayTool.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private TrayIconHost? _tray;
    private ProcessWatcherService? _processWatcher;
    private AudioSwitchWatcher? _audioWatcher;
    private PowerWatchService? _powerWatcher;

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
    public StartupRegistrationService StartupRegistration { get; } = new();

    public App()
    {
        InitializeComponent();
        DebugTool = new OculusDebugToolService(Oculus);
        Profiles = new ProfileService(Settings);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, MutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            System.Windows.MessageBox.Show(
                $"{AppName} is already running.\nCheck the notification area (system tray).",
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

        if (Settings.Current.ApplyGameSettingsOnStart && DebugTool.IsAvailable)
        {
            var result = DebugTool.Apply(Settings.Current.DefaultGameSettings);
            Log.Info(result.Summary);
        }

        if (Settings.Current.ApplyLinkSettingsOnStart)
        {
            var linkResult = Link.Apply(Settings.Current.LinkSettings);
            Log.Info(linkResult.Summary);
        }
        else
        {
            Log.Info(Link.DescribeRegistryStatus());
        }

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
    }

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

        _powerWatcher?.Dispose();
        _audioWatcher?.Dispose();
        _processWatcher?.Dispose();
        _tray?.Dispose();
        Settings.Save();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    public static string GetVersion()
    {
        return typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    }

    public string ApplyProfile(GameProfile profile)
    {
        var result = DebugTool.Apply(profile.Settings);
        var summary = result.Summary;
        if (profile.Link.HasAny)
        {
            var merged = profile.Link.Overlay(Settings.Current.LinkSettings);
            var link = Link.Apply(merged, deleteUnsetOverrides: true);
            summary += " " + (link.Succeeded
                ? profile.Link.Describe() + " (reconnect Link if the stream does not change)."
                : link.Summary);
        }

        return summary;
    }

    public string RestoreGlobalDefaults()
    {
        var result = DebugTool.Apply(Settings.Current.DefaultGameSettings);
        var link = Link.Apply(Settings.Current.LinkSettings, deleteUnsetOverrides: true);
        return $"{result.Summary} Restored global Link settings ({Settings.Current.LinkSettings.Describe()}). {link.Summary}";
    }
}
