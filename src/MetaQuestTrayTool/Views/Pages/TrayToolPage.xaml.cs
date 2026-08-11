using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

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
        SelectTheme(app.Tray.Theme);
        StatusText.Text = "Audio switcher restores desktop devices when the Quest Link headset endpoint disappears.";
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
