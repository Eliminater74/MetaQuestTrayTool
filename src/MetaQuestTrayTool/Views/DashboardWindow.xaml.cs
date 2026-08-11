using System.Windows;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{App.GetVersion()}  ·  Windows tray utility";
        SettingsPathText.Text = AppPaths.SettingsFile;
        Loaded += (_, _) =>
        {
            LogList.ItemsSource = App.Instance.Log.Entries;
            RefreshStatus();
            ScrollLogToEnd();
        };
        Closed += (_, _) => App.Instance.Log.EntryAdded -= OnLogAdded;

        App.Instance.Log.EntryAdded += OnLogAdded;
    }

    public void RefreshStatus()
    {
        App.Instance.Oculus.Refresh();
        var oculus = App.Instance.Oculus;
        var debugTool = App.Instance.DebugTool;
        var defaults = App.Instance.Settings.Current.DefaultGameSettings;

        InstallPathText.Text = oculus.IsInstalled
            ? $"Installed at {oculus.InstallPath}"
            : "Meta Quest / Oculus PC software was not detected.";
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {oculus.ServiceStatus}";
        GameSettingsText.Text = defaults.Describe();
        DebugToolText.Text = debugTool.IsAvailable
            ? "OculusDebugToolCLI found"
            : "OculusDebugToolCLI not found";
        LinkSettingsText.Text = App.Instance.Settings.Current.LinkSettings.Describe();
        LinkLiveText.Text = "Live: " + App.Instance.Link.ReadCurrent().Describe();
        LastApplyText.Text = debugTool.LastResult?.Summary
                             ?? App.Instance.Link.LastResult?.Summary
                             ?? "Right-click the tray icon for Link and game settings.";
    }

    private void StartService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Start);
    private void StopService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Stop);
    private void RestartService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Restart);

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProfilesWindow
        {
            Owner = this
        };
        window.Show();
    }

    private void LinkSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new LinkSettingsWindow
        {
            Owner = this
        };
        window.ShowDialog();
        RefreshStatus();
    }

    private void AudioSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new AudioSettingsWindow
        {
            Owner = this
        };
        window.ShowDialog();
        RefreshStatus();
    }

    private void PowerSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new PowerSettingsWindow
        {
            Owner = this
        };
        window.ShowDialog();
        RefreshStatus();
    }

    private void ApplyGameSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = App.Instance.DebugTool.Apply(App.Instance.Settings.Current.DefaultGameSettings);
        if (!result.CliFound || !result.Started)
        {
            App.Instance.Log.Error(result.Summary);
        }
        else if (result.LooksRejected)
        {
            App.Instance.Log.Warn(result.Summary);
        }
        else
        {
            App.Instance.Log.Info(result.Summary);
        }

        RefreshStatus();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        App.Instance.Log.Info("Status refreshed.");
    }

    private void OnLogAdded(object? sender, Models.LogEntry e) => ScrollLogToEnd();

    private void RunService(Func<string> action)
    {
        var result = action();
        App.Instance.Log.Info(result);
        RefreshStatus();
    }

    private void ScrollLogToEnd()
    {
        if (LogList.Items.Count == 0)
        {
            return;
        }

        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
