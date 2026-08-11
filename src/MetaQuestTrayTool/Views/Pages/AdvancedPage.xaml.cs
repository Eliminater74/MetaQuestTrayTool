using System.Windows;
using System.Windows.Controls;

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
            $"Profiles: {profiles}. Settings file: {Services.AppPaths.SettingsFile}";
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
}
