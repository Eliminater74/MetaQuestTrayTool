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
        StartAsAdminBox.IsChecked = App.Instance.Settings.Current.AutomaticElevation;
        ElevationStatusText.Text = App.Instance.StartupRegistration.DescribeStatus();
        var service = App.Instance.Settings.Current.Service;
        StartOnToolBox.IsChecked = service.StartServiceWhenToolStarts;
        StopOnExitBox.IsChecked = service.StopServiceWhenToolExits;
        RestartOnWakeBox.IsChecked = service.RestartServiceWhenComputerWakes
                                     || App.Instance.Settings.Current.Power.RestartServiceAfterSleep;
        var dash = App.Instance.Settings.Current.DashToSteamVr;
        DashOpenXrBox.IsChecked = dash.SwitchOpenXrToSteamVr;
        PreventDashBox.IsChecked = App.Instance.DashToSteamVr.IsPreventDashLaunchEnabled()
                                   || dash.PreferPreventDashLaunch;
        NoUpdatesWithDashBox.IsChecked = dash.AlsoSetNoUpdatesWithPreventDash;
        RestartOvrOnSteamVrExitBox.IsChecked = dash.RestartOvrServiceWhenSteamVrExits;
        LoadCoreChannelBox();
        DashPathText.Text = App.Instance.DashToSteamVr.DescribeSteamVrPaths();
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch();
        CoreChannelStatusText.Text = App.Instance.DashToSteamVr.DescribeCoreChannel();
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        UpdateServiceButtons();
        _loading = false;
    }

    private void LoadCoreChannelBox()
    {
        CoreChannelBox.Items.Clear();
        var live = App.Instance.DashToSteamVr.ReadCoreChannel();
        foreach (var channel in DashToSteamVrService.KnownCoreChannels)
        {
            CoreChannelBox.Items.Add(new ComboBoxItem
            {
                Content = channel switch
                {
                    "LIVE" => "LIVE — Stable",
                    "PublicTest" => "PublicTest — Public Test Channel (beta)",
                    "NO_UPDATES" => "NO_UPDATES — Block Meta updates",
                    _ => channel
                },
                Tag = channel
            });
        }

        if (!string.IsNullOrWhiteSpace(live)
            && DashToSteamVrService.KnownCoreChannels.All(c =>
                !string.Equals(c, live, StringComparison.OrdinalIgnoreCase)))
        {
            CoreChannelBox.Items.Add(new ComboBoxItem
            {
                Content = $"{live} — current (custom)",
                Tag = live
            });
        }

        for (var i = 0; i < CoreChannelBox.Items.Count; i++)
        {
            if (CoreChannelBox.Items[i] is ComboBoxItem { Tag: string tag }
                && string.Equals(tag, live, StringComparison.OrdinalIgnoreCase))
            {
                CoreChannelBox.SelectedIndex = i;
                return;
            }
        }

        if (CoreChannelBox.Items.Count > 0)
        {
            CoreChannelBox.SelectedIndex = 0;
        }
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

    private void OpenDebugTool_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.Oculus.ShowOculusDebugTool();
        ServiceStatusText.Text = summary;
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
        dash.SwitchOpenXrToSteamVr = DashOpenXrBox.IsChecked == true;
        dash.AlsoSetNoUpdatesWithPreventDash = NoUpdatesWithDashBox.IsChecked == true;
        dash.RestartOvrServiceWhenSteamVrExits = RestartOvrOnSteamVrExitBox.IsChecked == true;
        App.Instance.Settings.Save();
        App.Instance.DashToSteamVr.SyncSessionWatch();
        App.Instance.DashToSteamVr.SyncSteamVrExitWatch();
    }

    private void PreventDash_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var enabled = PreventDashBox.IsChecked == true;
        if (enabled
            && NoUpdatesWithDashBox.IsChecked != true
            && !string.Equals(
                App.Instance.DashToSteamVr.ReadCoreChannel(),
                "NO_UPDATES",
                StringComparison.OrdinalIgnoreCase))
        {
            var ask = System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                "Also set CoreChannel to NO_UPDATES while PreventDashLaunch is on?\n\n"
                + "Optional precaution (from OculusKiller): Meta won’t push PC client/runtime updates that can undo Dash tweaks.\n"
                + "Your current channel is remembered so you can restore Stable / PublicTest later.\n\n"
                + "Yes = set NO_UPDATES now\nNo = keep your current channel ("
                + (App.Instance.DashToSteamVr.ReadCoreChannel() ?? "unknown") + ")",
                App.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (ask == MessageBoxResult.Yes)
            {
                NoUpdatesWithDashBox.IsChecked = true;
                App.Instance.Settings.Current.DashToSteamVr.AlsoSetNoUpdatesWithPreventDash = true;
            }
        }

        App.Instance.Settings.Current.DashToSteamVr.PreferPreventDashLaunch = enabled;
        App.Instance.Settings.Save();
        // Write registry immediately (without service restart); user can click Apply for restart.
        var summary = App.Instance.DashToSteamVr.SetPreventDashLaunch(enabled, restartOvrService: false);
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch() + " " + summary;
        CoreChannelStatusText.Text = App.Instance.DashToSteamVr.DescribeCoreChannel();
        LoadCoreChannelBox();
        App.Instance.DashToSteamVr.SyncSessionWatch();
    }

    private void ApplyPreventDash_Click(object sender, RoutedEventArgs e)
    {
        var enabled = PreventDashBox.IsChecked == true;
        var summary = App.Instance.DashToSteamVr.SetPreventDashLaunch(enabled, restartOvrService: true);
        PreventDashStatusText.Text = App.Instance.DashToSteamVr.DescribePreventDashLaunch();
        CoreChannelStatusText.Text = App.Instance.DashToSteamVr.DescribeCoreChannel();
        LoadCoreChannelBox();
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        UpdateServiceButtons();
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            summary,
            App.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ApplyCoreChannel_Click(object sender, RoutedEventArgs e)
    {
        if (CoreChannelBox.SelectedItem is not ComboBoxItem { Tag: string channel })
        {
            return;
        }

        var summary = App.Instance.DashToSteamVr.SetCoreChannel(channel, restartOvrService: true);
        CoreChannelStatusText.Text = App.Instance.DashToSteamVr.DescribeCoreChannel();
        LoadCoreChannelBox();
        ServiceStatusText.Text = $"{OculusRuntimeService.ServiceName}: {App.Instance.Oculus.ServiceStatus}";
        UpdateServiceButtons();
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            summary,
            App.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RestoreCoreChannel_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.DashToSteamVr.RestoreCoreChannelBeforeNoUpdates(restartOvrService: true);
        CoreChannelStatusText.Text = App.Instance.DashToSteamVr.DescribeCoreChannel();
        LoadCoreChannelBox();
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
        App.Instance.Settings.Current.Power.RestartServiceAfterSleep = service.RestartServiceWhenComputerWakes;
        App.Instance.Settings.Save();
    }

    private void OpenSteamVrHome_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.SteamVrInstall.OpenSteamVrHome();
        App.Instance.Log.Info(summary);
        System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            summary,
            App.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Run(Func<string> action)
    {
        var result = action();
        App.Instance.Log.Info(result);
        Refresh();
    }
}
