using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class ServiceStartupPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public ServiceStartupPage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        _loading = true;
        App.Instance.Oculus.Refresh();
        App.Instance.StartupRegistration.SyncFromSystem(App.Instance.Settings.Current);
        StartAsAdminBox.IsChecked = App.Instance.Settings.Current.AutomaticElevation;
        ElevationStatusText.Text = App.Instance.StartupRegistration.DescribeStatus();
        var service = App.Instance.Settings.Current.Service;
        StartOnToolBox.IsChecked = service.StartServiceWhenToolStarts;
        StopOnExitBox.IsChecked = service.StopServiceWhenToolExits;
        RestartOnWakeBox.IsChecked = service.RestartServiceWhenComputerWakes
                                     || App.Instance.Settings.Current.Power.RestartServiceAfterSleep;
        LaunchHomeOnServiceBox.IsChecked = service.LaunchOculusHomeOnServiceStart;
        LaunchHomeOnToolBox.IsChecked = service.LaunchOculusHomeOnToolStart;
        CloseHomeOnExitBox.IsChecked = service.CloseOculusHomeOnToolExit;
        ServiceStatusText.Text = $"{Services.OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        _loading = false;
    }

    private void RestartElevated_Click(object sender, RoutedEventArgs e) =>
        StartupUiHelper.TryRestartElevated(Window.GetWindow(this));

    private void Start_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Start);
    private void Stop_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Stop);
    private void Restart_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Restart);

    private void Persist_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current;
        var startAsAdmin = StartAsAdminBox.IsChecked == true;
        if (settings.AutomaticElevation != startAsAdmin)
        {
            if (!StartupUiHelper.TryApply(
                    Window.GetWindow(this),
                    startWithWindows: startAsAdmin || settings.StartWithWindows,
                    asAdministrator: startAsAdmin))
            {
                _loading = true;
                StartAsAdminBox.IsChecked = settings.AutomaticElevation;
                _loading = false;
                return;
            }

            _loading = true;
            StartAsAdminBox.IsChecked = settings.AutomaticElevation;
            ElevationStatusText.Text = App.Instance.StartupRegistration.DescribeStatus();
            _loading = false;
        }

        var service = settings.Service;
        service.StartServiceWhenToolStarts = StartOnToolBox.IsChecked == true;
        service.StopServiceWhenToolExits = StopOnExitBox.IsChecked == true;
        service.RestartServiceWhenComputerWakes = RestartOnWakeBox.IsChecked == true;
        service.LaunchOculusHomeOnServiceStart = LaunchHomeOnServiceBox.IsChecked == true;
        service.LaunchOculusHomeOnToolStart = LaunchHomeOnToolBox.IsChecked == true;
        service.CloseOculusHomeOnToolExit = CloseHomeOnExitBox.IsChecked == true;
        App.Instance.Settings.Current.Power.RestartServiceAfterSleep = service.RestartServiceWhenComputerWakes;
        App.Instance.Settings.Save();
    }

    private void Run(Func<string> action)
    {
        var result = action();
        App.Instance.Log.Info(result);
        if (App.Instance.Settings.Current.Service.LaunchOculusHomeOnServiceStart
            && action == App.Instance.Oculus.Start)
        {
            TryLaunchHome();
        }

        Refresh();
    }

    public static void TryLaunchHome()
    {
        App.Instance.Oculus.Refresh();
        var install = App.Instance.Oculus.InstallPath;
        if (string.IsNullOrWhiteSpace(install))
        {
            App.Instance.Log.Warn("Cannot launch Oculus Home — install path unknown.");
            return;
        }

        var client = Path.Combine(install, "Support", "oculus-client", "OculusClient.exe");
        if (!File.Exists(client))
        {
            client = Path.Combine(install, "OculusClient.exe");
        }

        if (!File.Exists(client))
        {
            App.Instance.Log.Warn("OculusClient.exe was not found.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = client,
            UseShellExecute = true
        });
        App.Instance.Log.Info("Launched Oculus Home / client.");
    }

    public static void TryCloseHome()
    {
        foreach (var name in new[] { "OculusClient", "OculusDash", "ovrserver_x64" })
        {
            // Only close the client UI, not the runtime server.
            if (name is "ovrserver_x64")
            {
                continue;
            }

            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.CloseMainWindow();
                    App.Instance.Log.Info($"Requested close for {name}.");
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
