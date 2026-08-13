using System.Windows;
using System.Windows.Threading;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class InfoPage : System.Windows.Controls.UserControl, IShellPage
{
    private readonly DispatcherTimer _refreshTimer;
    private bool _fullReportLoaded;

    public InfoPage()
    {
        InitializeComponent();
        // Light banner refresh — ADB identity only every 3rd tick (expensive).
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        var tick = 0;
        _refreshTimer.Tick += (_, _) =>
        {
            if (IsVisible && IsLoaded)
            {
                tick++;
                RefreshBanners(includeAdb: tick % 3 == 0);
            }
        };
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    public void Refresh()
    {
        RefreshBanners(includeAdb: true);
        ReportBox.Text = "Building report…";
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsLoaded)
            {
                return;
            }

            ReportBox.Text = SystemInfoService.BuildReport(includeEnumHmd: false);
            _fullReportLoaded = true;
        });
    }

    private void RefreshBanners(bool includeAdb = true)
    {
        // No EnumHmd on the auto-refresh path — it can interfere with Air Link connect.
        var connection = App.Instance.LinkConnection.Probe(includeEnumHmd: false);
        ConnectionBanner.Text = $"Link: {connection.InfoBanner}";
        ConnectionBanner.Foreground = connection.SessionActive
            ? (System.Windows.Media.Brush)FindResource("AppAccentBrush")
            : (System.Windows.Media.Brush)FindResource("AppMutedBrush");

        var openXr = App.Instance.OpenXr.ReadActiveKind();
        OpenXrBanner.Text = $"OpenXR: {OpenXrRuntimeService.Label(openXr)}";

        if (includeAdb)
        {
            var headset = App.Instance.Headset.ReadIdentity(App.Instance.Settings.Current.Headset);
            HeadsetBanner.Text = $"ADB: {headset.DescribeAdbBanner(connection)}";
            HeadsetBanner.Foreground = headset.IsRogue || headset.IsIgnored
                ? System.Windows.Media.Brushes.OrangeRed
                : headset.IsReady
                    ? (System.Windows.Media.Brush)FindResource("AppTextBrush")
                    : (System.Windows.Media.Brush)FindResource("AppMutedBrush");
        }

        var steamTip = App.Instance.SteamLinkAssist.DescribeOpenXrMismatch(connection);
        if (string.IsNullOrWhiteSpace(steamTip))
        {
            SteamTipBanner.Visibility = Visibility.Collapsed;
            SteamTipBanner.Text = string.Empty;
        }
        else
        {
            SteamTipBanner.Visibility = Visibility.Visible;
            SteamTipBanner.Text = steamTip;
        }

        if (!_fullReportLoaded && string.IsNullOrWhiteSpace(ReportBox.Text))
        {
            ReportBox.Text = "Click Refresh for the full report.";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Donate_Click(object sender, RoutedEventArgs e) =>
        DonateService.Open(Window.GetWindow(this));

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(ReportBox.Text ?? string.Empty);
        App.Instance.Log.Info("Copied Info report to the clipboard.");
    }

    private void Trust_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = App.Instance.Headset.TrustCurrentHeadset(App.Instance.Settings.Current.Headset);
            App.Instance.Settings.Save();
            App.Instance.Log.Info(result);
            Refresh();
            System.Windows.MessageBox.Show(Window.GetWindow(this), result, App.AppName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this), ex.Message, App.AppName);
        }
    }
}
