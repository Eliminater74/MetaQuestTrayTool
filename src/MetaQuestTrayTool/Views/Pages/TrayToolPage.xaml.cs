using System.Windows;
using System.Windows.Controls;

namespace MetaQuestTrayTool.Views.Pages;

public partial class TrayToolPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public TrayToolPage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        _loading = true;
        var app = App.Instance.Settings.Current;
        StartWithWindowsBox.IsChecked = app.StartWithWindows;
        StartMinimizedBox.IsChecked = app.Tray.StartMinimized;
        AudioSwitchBox.IsChecked = app.Audio.AutoSwitchEnabled;
        HideAltTabBox.IsChecked = app.Tray.HideFromAltTab;
        MinimizeOnCloseBox.IsChecked = app.Tray.MinimizeOnClose;
        HotKeysBox.IsChecked = app.Tray.EnableHotKeys;
        UpdatesBox.IsChecked = app.Tray.CheckForUpdatesOnStart;
        NotificationsBox.IsChecked = app.ShowNotifications;
        StatusText.Text = "Audio switcher restores desktop devices when the Quest Link headset endpoint disappears.";
        _loading = false;
    }

    private void Persist_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current;
        var startWithWindows = StartWithWindowsBox.IsChecked == true;
        if (settings.StartWithWindows != startWithWindows)
        {
            try
            {
                App.Instance.StartupRegistration.SetEnabled(startWithWindows);
                settings.StartWithWindows = startWithWindows;
            }
            catch (Exception ex)
            {
                App.Instance.Log.Error("Could not update Start with Windows.", ex);
                _loading = true;
                StartWithWindowsBox.IsChecked = settings.StartWithWindows;
                _loading = false;
                System.Windows.MessageBox.Show(
                    Window.GetWindow(this),
                    $"Could not update the Windows startup entry.\n\n{ex.Message}",
                    App.AppName);
                return;
            }
        }

        settings.Tray.StartMinimized = StartMinimizedBox.IsChecked == true;
        settings.Audio.AutoSwitchEnabled = AudioSwitchBox.IsChecked == true;
        settings.Tray.HideFromAltTab = HideAltTabBox.IsChecked == true;
        settings.Tray.MinimizeOnClose = MinimizeOnCloseBox.IsChecked == true;
        settings.Tray.CheckForUpdatesOnStart = UpdatesBox.IsChecked == true;
        settings.ShowNotifications = NotificationsBox.IsChecked == true;
        App.Instance.Settings.Save();

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
}
