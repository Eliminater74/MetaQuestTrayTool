using System.Windows;

namespace MetaQuestTrayTool.Views.Pages;

public partial class AdvancedPage : System.Windows.Controls.UserControl, IShellPage
{
    public AdvancedPage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        var profiles = App.Instance.Settings.Current.Profiles.Count;
        StatusText.Text =
            $"Profiles: {profiles}. Settings: {Services.AppPaths.SettingsFile}  ·  Profiles file: {Services.ProfileStore.ProfilesFile}";
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            "Reset all settings to defaults?\n\nProfiles will be kept. Choose Remove all under Profiles to clear those separately.",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        App.Instance.Settings.ResetKeepingProfiles();
        App.Instance.ReloadFeatureWatchers();
        App.Instance.Log.Info("Reset settings to defaults (profiles preserved).");
        Refresh();
        System.Windows.MessageBox.Show(Window.GetWindow(this), "Settings reset. Profiles were kept.", App.AppName);
    }

    private void RemoveProfiles_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            "Remove all personal profiles?",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        App.Instance.Settings.Current.Profiles.Clear();
        App.Instance.Settings.Save();
        App.Instance.Log.Info("Removed all profiles.");
        Refresh();
    }

    private void Library_Click(object sender, RoutedEventArgs e)
    {
        var window = new LibraryPickerWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProfilesWindow { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        var window = new GlobalDefaultsWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void Audio_Click(object sender, RoutedEventArgs e)
    {
        var window = new AudioSettingsWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void OpenDebugTool_Click(object sender, RoutedEventArgs e)
    {
        var summary = App.Instance.Oculus.ShowOculusDebugTool();
        StatusText.Text = summary;
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

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Meta Quest Tray Tool settings",
            Filter = "Settings backup (*.json)|*.json",
            FileName = $"mqtt-settings-{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            App.Instance.Settings.Export(dialog.FileName);
            App.Instance.Log.Info("Exported settings to " + dialog.FileName);
            StatusText.Text = "Exported: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this), ex.Message, App.AppName);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Meta Quest Tray Tool settings",
            Filter = "Settings backup (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            "Replace current settings and profiles with this backup?",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            App.Instance.Settings.Import(dialog.FileName);
            App.Instance.ReloadFeatureWatchers();
            App.Instance.Log.Info("Imported settings from " + dialog.FileName);
            Refresh();
            StatusText.Text = "Imported. Hotkeys, voice, announcements, and watchers were reloaded.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this), ex.Message, App.AppName);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking GitHub for updates…";
        await App.Instance.Updates.CheckInteractivelyAsync(Window.GetWindow(this), quietIfUpToDate: false);
        StatusText.Text = "Update check finished.";
    }
}
