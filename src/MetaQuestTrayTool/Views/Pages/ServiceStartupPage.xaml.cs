using System.Diagnostics;
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
        var dash = App.Instance.Settings.Current.DashToSteamVr;
        DashAutoBox.IsChecked = dash.AutoOnMetaLinkConnect;
        DashOpenXrBox.IsChecked = dash.SwitchOpenXrToSteamVr;
        DashReaperBox.IsChecked = dash.KeepKillingDashWhileSteamVr;
        DashCloseClientBox.IsChecked = dash.CloseMetaClient;
        PreventDashBox.IsChecked = App.Instance.DashToSteamVr.IsPreventDashLaunchEnabled()
                                   || dash.PreferPreventDashLaunch;
        DashPathText.Text = App.Instance.DashToSteamVr.DescribeSteamVrPaths();
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch();
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        UpdateServiceButtons();
        _loading = false;
    }

    private void UpdateServiceButtons()
    {
        var oculus = App.Instance.Oculus;
        var running = oculus.IsServiceRunning;
        var exists = oculus.ServiceExists;

        StartButton.IsEnabled = exists && !running;
        StopButton.IsEnabled = exists && running;
        RestartButton.IsEnabled = exists;

        StartButton.Style = running || !exists
            ? (Style)FindResource("GhostButtonStyle")
            : (Style)FindResource("AccentButtonStyle");
        StopButton.Style = running
            ? (Style)FindResource("AccentButtonStyle")
            : (Style)FindResource("GhostButtonStyle");
        RestartButton.Style = (Style)FindResource("GhostButtonStyle");
    }

    private void RestartElevated_Click(object sender, RoutedEventArgs e) =>
        StartupUiHelper.TryRestartElevated(Window.GetWindow(this));

    private void Start_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Start);
    private void Stop_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Stop);
    private void Restart_Click(object sender, RoutedEventArgs e) => Run(App.Instance.Oculus.Restart);

    private void OpenMetaLink_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.Oculus.ShowMetaHorizonLink();
        if (summary.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Could not", StringComparison.OrdinalIgnoreCase))
        {
            App.Instance.Log.Warn(summary);
        }
        else
        {
            App.Instance.Log.Info(summary);
        }
    }

    private void DashToSteamVr_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.DashToSteamVr.RunNow("Service & Startup button");
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            summary,
            App.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PersistDash_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var dash = App.Instance.Settings.Current.DashToSteamVr;
        dash.AutoOnMetaLinkConnect = DashAutoBox.IsChecked == true;
        dash.SwitchOpenXrToSteamVr = DashOpenXrBox.IsChecked == true;
        dash.KeepKillingDashWhileSteamVr = DashReaperBox.IsChecked == true;
        dash.CloseMetaClient = DashCloseClientBox.IsChecked == true;
        App.Instance.Settings.Save();
    }

    private void PreventDash_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var enabled = PreventDashBox.IsChecked == true;
        App.Instance.Settings.Current.DashToSteamVr.PreferPreventDashLaunch = enabled;
        App.Instance.Settings.Save();
        // Write registry immediately (without service restart); user can click Apply for restart.
        var summary = App.Instance.DashToSteamVr.SetPreventDashLaunch(enabled, restartOvrService: false);
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch() + " " + summary;
    }

    private void ApplyPreventDash_Click(object sender, RoutedEventArgs e)
    {
        var enabled = PreventDashBox.IsChecked == true;
        var summary = App.Instance.DashToSteamVr.SetPreventDashLaunch(enabled, restartOvrService: true);
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch();
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        UpdateServiceButtons();
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            summary,
            App.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

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
        var summary = App.Instance.Oculus.ShowMetaHorizonLink();
        if (summary.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Could not", StringComparison.OrdinalIgnoreCase))
        {
            App.Instance.Log.Warn(summary);
        }
        else
        {
            App.Instance.Log.Info(summary);
        }
    }

    public static void TryCloseHome()
    {
        var clientPath = App.Instance.Oculus.ResolveClientExePath();
        foreach (var name in new[] { "Client", "OculusClient", "OculusDash" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    using (process)
                    {
                        if (name.Equals("Client", StringComparison.OrdinalIgnoreCase))
                        {
                            string? path = null;
                            try
                            {
                                path = process.MainModule?.FileName;
                            }
                            catch
                            {
                                continue;
                            }

                            if (path is null
                                || (clientPath is not null
                                    && !string.Equals(path, clientPath, StringComparison.OrdinalIgnoreCase)
                                    && !path.Contains("oculus-client", StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }

                        process.CloseMainWindow();
                        App.Instance.Log.Info($"Requested close for {name}.");
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
