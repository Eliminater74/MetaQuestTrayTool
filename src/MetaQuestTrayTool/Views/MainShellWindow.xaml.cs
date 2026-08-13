using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Views.Pages;

namespace MetaQuestTrayTool.Views;

public partial class MainShellWindow : Window
{
    private readonly GameSettingsPage _gamePage = new();
    private readonly TrayToolPage _trayPage = new();
    private readonly PowerOptionsPage _powerPage = new();
    private readonly ServiceStartupPage _servicePage = new();
    private readonly LogWindowPage _logPage = new();
    private readonly AdvancedPage _advancedPage = new();
    private readonly QuestLinkPage _linkPage = new();
    private readonly HeadsetPage _headsetPage = new();
    private readonly InfoPage _infoPage = new();
    private readonly VrToolsPage _vrToolsPage = new();
    private bool _forceClose;

    public MainShellWindow()
    {
        InitializeComponent();
        AuthorText.Text = $"By {AppInfo.Author}";
        VersionText.Text = $"v{App.GetVersion()}";
        ApplyAltTabPreference();
        NavGame.IsChecked = true;
    }

    public void ShowPage(string tag)
    {
        var nav = tag switch
        {
            "Tray" => NavTray,
            "Power" => NavPower,
            "Service" => NavService,
            "Log" => NavLog,
            "Advanced" => NavAdvanced,
            "Link" => NavLink,
            "Headset" => NavHeadset,
            "Info" => NavInfo,
            "VrTools" => NavVrTools,
            _ => NavGame
        };
        nav.IsChecked = true;
        Activate();
        WindowState = WindowState.Normal;
    }

    public void RefreshActivePage()
    {
        if (PageHost.Content is IShellPage page)
        {
            page.Refresh();
        }

        UpdateLogNavLabel();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton { Tag: string tag })
        {
            return;
        }

        System.Windows.Controls.UserControl page = tag switch
        {
            "Tray" => _trayPage,
            "Power" => _powerPage,
            "Service" => _servicePage,
            "Log" => _logPage,
            "Advanced" => _advancedPage,
            "Link" => _linkPage,
            "Headset" => _headsetPage,
            "Info" => _infoPage,
            "VrTools" => _vrToolsPage,
            _ => _gamePage
        };

        PageHeaderText.Text = tag switch
        {
            "Tray" => "Tray Tool",
            "Power" => "Power Options",
            "Service" => "Service & Startup",
            "Log" => "Log Window",
            "Advanced" => "Advanced",
            "Link" => "Quest Link",
            "Headset" => "Headset",
            "Info" => "Info",
            "VrTools" => "VR Tools",
            _ => "Game Settings"
        };

        // Paint the page first, then refresh off the click handler so sidebar feels instant.
        // Some Refresh() paths hit ADB / Link probe / ServiceController on the UI thread.
        PageHost.Content = page;
        UpdateLogNavLabel();
        if (page is IShellPage shellPage)
        {
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                shellPage.Refresh);
        }
    }

    private void UpdateLogNavLabel()
    {
        var count = App.Instance.Log.Entries.Count;
        NavLog.Content = count > 0 ? $"Log Window ({count})" : "Log Window";
    }

    private void Donate_Click(object sender, RoutedEventArgs e) =>
        Services.DonateService.Open(this);

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        if (App.Instance.Settings.Current.Tray.MinimizeOnClose)
        {
            e.Cancel = true;
            Hide();
            App.Instance.NotifyStillRunningInTray();
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "Exit Meta Quest Tray Tool completely?\n\nChoose No to keep it running in the tray.",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            Hide();
            App.Instance.NotifyStillRunningInTray();
        }
        else
        {
            App.Instance.Shutdown();
        }
    }

    public void ApplyAltTabPreference()
    {
        ShowInTaskbar = !App.Instance.Settings.Current.Tray.HideFromAltTab;
    }
}
