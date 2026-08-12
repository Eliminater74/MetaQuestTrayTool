using System.Windows;
using System.Windows.Threading;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class InfoPage : System.Windows.Controls.UserControl, IShellPage
{
    private readonly DispatcherTimer _refreshTimer;

    public InfoPage()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _refreshTimer.Tick += (_, _) =>
        {
            if (IsVisible && IsLoaded)
            {
                Refresh();
            }
        };
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    public void Refresh()
    {
        // No EnumHmd on the auto-refresh path — it can interfere with Air Link connect.
        var connection = App.Instance.LinkConnection.Probe(includeEnumHmd: false);
        ConnectionBanner.Text = $"Link: {connection.InfoBanner}";
        ConnectionBanner.Foreground = connection.SessionActive
            ? (System.Windows.Media.Brush)FindResource("AppAccentBrush")
            : (System.Windows.Media.Brush)FindResource("AppMutedBrush");

        var openXr = App.Instance.OpenXr.ReadActiveKind();
        OpenXrBanner.Text = $"OpenXR: {OpenXrRuntimeService.Label(openXr)}";

        var headset = App.Instance.Headset.ReadIdentity(App.Instance.Settings.Current.Headset);
        HeadsetBanner.Text = $"ADB: {headset.DescribeAdbBanner(connection)}";
        HeadsetBanner.Foreground = headset.IsRogue || headset.IsIgnored
            ? System.Windows.Media.Brushes.OrangeRed
            : headset.IsReady
                ? (System.Windows.Media.Brush)FindResource("AppTextBrush")
                : (System.Windows.Media.Brush)FindResource("AppMutedBrush");

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

        ReportBox.Text = SystemInfoService.BuildReport(includeEnumHmd: false);
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
