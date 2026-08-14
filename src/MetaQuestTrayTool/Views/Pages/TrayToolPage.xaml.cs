using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;
using MetaQuestTrayTool.Views;

namespace MetaQuestTrayTool.Views.Pages;

public partial class TrayToolPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public TrayToolPage()
    {
        InitializeComponent();
        ThemeBox.Items.Add(new ComboBoxItem { Content = "Pure Black", Tag = AppTheme.Black });
        ThemeBox.Items.Add(new ComboBoxItem { Content = "Dark", Tag = AppTheme.Dark });
        ThemeBox.Items.Add(new ComboBoxItem { Content = "Light", Tag = AppTheme.Light });

        foreach (UpdateCheckInterval interval in Enum.GetValues<UpdateCheckInterval>())
        {
            UpdateIntervalBox.Items.Add(new ComboBoxItem
            {
                Content = UpdateCheckIntervalHelper.Describe(interval),
                Tag = interval
            });
        }
    }

    public void Refresh()
    {
        _loading = true;
        var app = App.Instance.Settings.Current;
        StartWithWindowsBox.IsChecked = app.StartWithWindows;
        StartAsAdminBox.IsChecked = app.AutomaticElevation;
        StartMinimizedBox.IsChecked = app.Tray.StartMinimized;
        AudioSwitchBox.IsChecked = app.Audio.AutoSwitchEnabled;
        HideAltTabBox.IsChecked = app.Tray.HideFromAltTab;
        MinimizeOnCloseBox.IsChecked = app.Tray.MinimizeOnClose;
        HotKeysBox.IsChecked = app.HotKeys.Enabled;
        VoiceBox.IsChecked = app.Voice.Enabled;
        UpdatesBox.IsChecked = app.Tray.CheckForUpdatesOnStart;
        SelectUpdateInterval(app.Tray.AutoUpdateCheckInterval);
        NotificationsBox.IsChecked = app.ShowNotifications;
        SelectTheme(app.Tray.Theme);
        StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                          + " Voice: " + App.Instance.Voice.Status
                          + " " + App.Instance.Updates.DescribeSchedule();
        _loading = false;
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        if (ThemeBox.SelectedItem is not ComboBoxItem { Tag: AppTheme theme })
        {
            return;
        }

        App.Instance.Settings.Current.Tray.Theme = theme;
        App.Instance.Settings.Save();
        ThemeService.Apply(theme);
        App.Instance.Log.Info($"Theme set to {theme}.");
    }

    private void SelectTheme(AppTheme theme)
    {
        foreach (ComboBoxItem item in ThemeBox.Items)
        {
            if (item.Tag is AppTheme value && value == theme)
            {
                ThemeBox.SelectedItem = item;
                return;
            }
        }

        ThemeBox.SelectedIndex = 0;
    }

    private void SelectUpdateInterval(UpdateCheckInterval interval)
    {
        foreach (ComboBoxItem item in UpdateIntervalBox.Items)
        {
            if (item.Tag is UpdateCheckInterval value && value == interval)
            {
                UpdateIntervalBox.SelectedItem = item;
                return;
            }
        }

        UpdateIntervalBox.SelectedIndex = (int)UpdateCheckInterval.Weekly;
    }

    private void UpdateInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        if (UpdateIntervalBox.SelectedItem is not ComboBoxItem { Tag: UpdateCheckInterval interval })
        {
            return;
        }

        App.Instance.Settings.Current.Tray.AutoUpdateCheckInterval = interval;
        App.Instance.Settings.Save();
        StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                          + " Voice: " + App.Instance.Voice.Status
                          + " " + App.Instance.Updates.DescribeSchedule();
        App.Instance.Log.Info($"Auto update check set to {UpdateCheckIntervalHelper.Describe(interval)}.");
    }

    private void Persist_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current;
        var startWithWindows = StartWithWindowsBox.IsChecked == true;
        var startAsAdmin = StartAsAdminBox.IsChecked == true;
        if (settings.StartWithWindows != startWithWindows
            || settings.AutomaticElevation != startAsAdmin)
        {
            if (!StartupUiHelper.TryApply(Window.GetWindow(this), startWithWindows, startAsAdmin))
            {
                _loading = true;
                StartWithWindowsBox.IsChecked = settings.StartWithWindows;
                StartAsAdminBox.IsChecked = settings.AutomaticElevation;
                _loading = false;
                return;
            }

            _loading = true;
            StartWithWindowsBox.IsChecked = settings.StartWithWindows;
            StartAsAdminBox.IsChecked = settings.AutomaticElevation;
            StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                              + " Audio switcher leaves your boot/desktop devices alone until a PCVR session starts, then restores them when it ends.";
            _loading = false;
        }

        settings.Tray.StartMinimized = StartMinimizedBox.IsChecked == true;
        settings.Audio.AutoSwitchEnabled = AudioSwitchBox.IsChecked == true;
        settings.Tray.HideFromAltTab = HideAltTabBox.IsChecked == true;
        settings.Tray.MinimizeOnClose = MinimizeOnCloseBox.IsChecked == true;
        settings.Tray.CheckForUpdatesOnStart = UpdatesBox.IsChecked == true;
        settings.ShowNotifications = NotificationsBox.IsChecked == true;
        App.Instance.Settings.Save();
        App.Instance.AudioWatch?.SyncTimer();

        if (Window.GetWindow(this) is MainShellWindow shell)
        {
            shell.ApplyAltTabPreference();
        }
    }

    private void AudioConfigure_Click(object sender, RoutedEventArgs e)
    {
        var window = new AudioSettingsWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void HotKeysConfigure_Click(object sender, RoutedEventArgs e)
    {
        var window = new HotKeysWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void HotKeys_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var enabled = HotKeysBox.IsChecked == true;
        App.Instance.Settings.Current.HotKeys.Enabled = enabled;
        App.Instance.Settings.Current.Tray.EnableHotKeys = enabled;
        App.Instance.Settings.Save();
        App.Instance.HotKeys.Reload();
        StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                          + " Voice: " + App.Instance.Voice.Status;
        App.Instance.Log.Info(enabled ? "HotKeys enabled." : "HotKeys disabled.");
    }

    private void VoiceConfigure_Click(object sender, RoutedEventArgs e)
    {
        var window = new VoiceCommandsWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void Voice_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var enabled = VoiceBox.IsChecked == true;
        App.Instance.Settings.Current.Voice.Enabled = enabled;
        App.Instance.Settings.Save();
        App.Instance.Voice.Reload();
        StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                          + " Voice: " + App.Instance.Voice.Status;
        App.Instance.Log.Info(enabled ? "Voice commands enabled." : "Voice commands disabled.");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking GitHub for updates…";
        await App.Instance.Updates.CheckInteractivelyAsync(Window.GetWindow(this), quietIfUpToDate: false);
        StatusText.Text = App.Instance.StartupRegistration.DescribeStatus()
                          + " Voice: " + App.Instance.Voice.Status
                          + " " + App.Instance.Updates.DescribeSchedule();
    }
}
