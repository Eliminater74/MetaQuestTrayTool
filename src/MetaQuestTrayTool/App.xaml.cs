using System.Windows;
using MetaQuestTrayTool.Services;
using MetaQuestTrayTool.Tray;

namespace MetaQuestTrayTool;

public partial class App : System.Windows.Application
{
    public const string AppName = "Meta Quest Tray Tool";
    private const string MutexName = @"Local\MetaQuestTrayTool.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private TrayIconHost? _tray;

    public static App Instance => (App)Current;
    public SettingsService Settings { get; } = new();
    public LogService Log { get; } = new();
    public OculusRuntimeService Oculus { get; } = new();
    public OculusDebugToolService DebugTool { get; }
    public StartupRegistrationService StartupRegistration { get; } = new();

    public App()
    {
        InitializeComponent();
        DebugTool = new OculusDebugToolService(Oculus);
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
        Log.Info($"{AppName} {GetVersion()} started.");
        Oculus.Refresh();
        Log.Info(Oculus.DescribeStatus());

        _tray = new TrayIconHost(this);
        _tray.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
}
