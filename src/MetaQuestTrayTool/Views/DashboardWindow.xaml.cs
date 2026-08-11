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

        App.Instance.Log.EntryAdded += (_, _) => ScrollLogToEnd();
    }

    public void RefreshStatus()
    {
        App.Instance.Oculus.Refresh();
        var oculus = App.Instance.Oculus;
        InstallPathText.Text = oculus.IsInstalled
            ? $"Installed at {oculus.InstallPath}"
            : "Meta Quest / Oculus PC software was not detected.";
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {oculus.ServiceStatus}";
    }

    private void StartService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Start);
    private void StopService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Stop);
    private void RestartService_Click(object sender, RoutedEventArgs e) => RunService(App.Instance.Oculus.Restart);

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        App.Instance.Log.Info("Status refreshed.");
    }

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
