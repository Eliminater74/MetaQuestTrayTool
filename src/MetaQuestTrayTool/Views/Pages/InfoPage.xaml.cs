using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;
using MediaBrush = System.Windows.Media.Brush;

namespace MetaQuestTrayTool.Views.Pages;

public partial class InfoPage : System.Windows.Controls.UserControl, IShellPage
{
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<ReadyItemVm> _readyItems = [];
    private bool _fullReportLoaded;

    public InfoPage()
    {
        InitializeComponent();
        ReadyItemsList.ItemsSource = _readyItems;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        var tick = 0;
        _refreshTimer.Tick += (_, _) =>
        {
            if (IsVisible && IsLoaded)
            {
                tick++;
                RefreshBanners(includeAdb: tick % 3 == 0);
                RefreshReadyChecklist();
            }
        };
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    public void Refresh()
    {
        RefreshBanners(includeAdb: true);
        RefreshReadyChecklist();
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

    private void RefreshReadyChecklist()
    {
        var report = App.Instance.PcvrReady.Evaluate();
        ReadySummaryText.Text = report.Summary;
        ReadySummaryText.Foreground = BrushFor(report.Overall);

        _readyItems.Clear();
        foreach (var item in report.Items)
        {
            _readyItems.Add(ReadyItemVm.From(item));
        }
    }

    private void RefreshBanners(bool includeAdb = true)
    {
        var connection = App.Instance.LinkConnection.Probe(includeEnumHmd: false);
        ConnectionBanner.Text = $"Link: {connection.InfoBanner}";
        ConnectionBanner.Foreground = connection.SessionActive
            ? (MediaBrush)FindResource("AppAccentBrush")
            : (MediaBrush)FindResource("AppMutedBrush");

        var steamVr = App.Instance.SteamVrInstall.Probe();
        SteamVrBanner.Text = steamVr.Banner;
        SteamVrBanner.Foreground = !steamVr.IsInstalled
            ? System.Windows.Media.Brushes.OrangeRed
            : steamVr.Channel == SteamVrChannel.Beta
                ? System.Windows.Media.Brushes.Orange
                : (MediaBrush)FindResource("AppAccentBrush");
        InstallSteamVrButton.Visibility = steamVr.IsInstalled ? Visibility.Collapsed : Visibility.Visible;

        var gpu = App.Instance.Gpu.GetRecommendation();
        GpuBanner.Text = gpu is null
            ? "GPU: not detected"
            : $"GPU: {gpu.Banner}";
        GpuBanner.Foreground = gpu is null
            ? (MediaBrush)FindResource("AppMutedBrush")
            : (MediaBrush)FindResource("AppAccentBrush");

        var openXr = App.Instance.OpenXr.ReadActiveKind();
        OpenXrBanner.Text = $"OpenXR: {OpenXrRuntimeService.Label(openXr)}";

        if (includeAdb)
        {
            var headset = App.Instance.Headset.ReadIdentity(App.Instance.Settings.Current.Headset);
            HeadsetBanner.Text = $"ADB: {headset.DescribeAdbBanner(connection)}";
            HeadsetBanner.Foreground = headset.IsRogue || headset.IsIgnored
                ? System.Windows.Media.Brushes.OrangeRed
                : headset.IsReady
                    ? (MediaBrush)FindResource("AppTextBrush")
                    : (MediaBrush)FindResource("AppMutedBrush");
        }

        var steamTip = App.Instance.SteamLinkAssist.DescribeOpenXrMismatch(connection);
        if (string.IsNullOrWhiteSpace(steamTip))
        {
            SteamTipBanner.Visibility = Visibility.Collapsed;
            SteamTipBanner.Text = string.Empty;
            FixSteamOpenXrButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SteamTipBanner.Visibility = Visibility.Visible;
            SteamTipBanner.Text = steamTip;
            FixSteamOpenXrButton.Visibility = Visibility.Visible;
        }

        var steamVrHint = SteamVrSettingsHintService.DescribeHints(connection);
        if (string.IsNullOrWhiteSpace(steamVrHint))
        {
            SteamVrHintBanner.Visibility = Visibility.Collapsed;
            SteamVrHintBanner.Text = string.Empty;
        }
        else
        {
            SteamVrHintBanner.Visibility = Visibility.Visible;
            SteamVrHintBanner.Text = steamVrHint;
        }

        if (App.Instance.SessionRecover.ShouldSuggestRecover(connection))
        {
            RecoverBanner.Visibility = Visibility.Visible;
            RecoverBannerText.Text = App.Instance.SessionRecover.DescribeSuggestion();
        }
        else
        {
            RecoverBanner.Visibility = Visibility.Collapsed;
            RecoverBannerText.Text = string.Empty;
        }

        if (!_fullReportLoaded && string.IsNullOrWhiteSpace(ReportBox.Text))
        {
            ReportBox.Text = "Click Refresh for the full report.";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void RefreshReady_Click(object sender, RoutedEventArgs e) => RefreshReadyChecklist();

    private void FixAllReady_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.PcvrReady.FixAll();
        App.Instance.Log.Info("PCVR Ready Fix all:\n" + summary);
        Refresh();
        System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
    }

    private void FixReadyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string id })
        {
            return;
        }

        var summary = App.Instance.PcvrReady.Fix(id);
        App.Instance.Log.Info($"PCVR Ready fix [{id}]: {summary}");
        RefreshReadyChecklist();
        System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
    }

    private void InstallSteamVr_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.SteamVrInstall.OpenInstallPage();
        App.Instance.Log.Info(summary);
        RefreshBanners(includeAdb: false);
        System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
    }

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

    private void ApplyGpu_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.ApplyGpuRecommendedPresets();
        Refresh();
        System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
    }

    private void RecoverSession_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.SessionRecover.Recover("Info page");
        Refresh();
        System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
    }

    private void FixSteamOpenXr_Click(object sender, RoutedEventArgs e)
    {
        if (!App.Instance.OpenXr.IsAvailable(OpenXrRuntimeKind.SteamVr))
        {
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                "SteamVR OpenXR runtime was not found. Install SteamVR from Steam first.",
                App.AppName);
            return;
        }

        var result = App.Instance.OpenXr.Set(OpenXrRuntimeKind.SteamVr);
        App.Instance.Log.Info(result);
        Refresh();
        System.Windows.MessageBox.Show(Window.GetWindow(this), result, App.AppName);
    }

    private MediaBrush BrushFor(PcvrReadyLevel level) => level switch
    {
        PcvrReadyLevel.Ok => (MediaBrush)FindResource("AppAccentBrush"),
        PcvrReadyLevel.Warn => System.Windows.Media.Brushes.Orange,
        _ => System.Windows.Media.Brushes.OrangeRed
    };

    private sealed class ReadyItemVm
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Detail { get; init; }
        public required string LevelLabel { get; init; }
        public required MediaBrush LevelBrush { get; init; }
        public required string FixLabel { get; init; }
        public required Visibility FixVisibility { get; init; }

        public static ReadyItemVm From(PcvrReadyItem item) => new()
        {
            Id = item.Id,
            Title = item.Title,
            Detail = item.Detail,
            LevelLabel = item.Level switch
            {
                PcvrReadyLevel.Ok => "OK",
                PcvrReadyLevel.Warn => "WARN",
                _ => "FIX"
            },
            LevelBrush = item.Level switch
            {
                PcvrReadyLevel.Ok => (MediaBrush)System.Windows.Application.Current.FindResource("AppAccentBrush"),
                PcvrReadyLevel.Warn => System.Windows.Media.Brushes.Orange,
                _ => System.Windows.Media.Brushes.OrangeRed
            },
            FixLabel = item.FixLabel ?? "Fix",
            FixVisibility = item.CanFix ? Visibility.Visible : Visibility.Collapsed
        };
    }
}
