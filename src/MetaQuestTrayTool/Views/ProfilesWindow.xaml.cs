using System.Windows;
using System.Windows.Input;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class ProfilesWindow : Window
{
    public ProfilesWindow()
    {
        InitializeComponent();
        Reload();
    }

    public void Reload()
    {
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = App.Instance.Profiles.All
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new ProfileRow(profile))
            .ToList();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var profile = new GameProfile
        {
            Settings = App.Instance.Settings.Current.DefaultGameSettings.Clone()
        };

        if (Edit(profile) == true)
        {
            App.Instance.Profiles.Add(profile);
            App.Instance.Log.Info($"Created profile '{profile.Name}' for {profile.ProcessName}.");
            Reload();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void ProfileList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile() is not { } profile)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            this,
            $"Delete profile '{profile.Name}'?",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        App.Instance.Profiles.Remove(profile);
        App.Instance.Log.Info($"Deleted profile '{profile.Name}'.");
        Reload();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile() is not { } profile)
        {
            return;
        }

        var result = App.Instance.DebugTool.Apply(profile.Settings);
        App.Instance.Log.Info($"Applied profile '{profile.Name}': {result.Summary}");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void EditSelected()
    {
        if (SelectedProfile() is not { } profile)
        {
            return;
        }

        if (Edit(profile) == true)
        {
            App.Instance.Profiles.Save();
            App.Instance.Log.Info($"Updated profile '{profile.Name}'.");
            Reload();
        }
    }

    private bool? Edit(GameProfile profile)
    {
        var editor = new ProfileEditorWindow(profile)
        {
            Owner = this
        };
        return editor.ShowDialog();
    }

    private GameProfile? SelectedProfile() =>
        ProfileList.SelectedItem is ProfileRow row ? row.Profile : null;

    private sealed class ProfileRow(GameProfile profile)
    {
        public GameProfile Profile { get; } = profile;

        public override string ToString() =>
            $"{Profile.Name}  ·  {Profile.ProcessName}  ·  {Profile.Settings.Describe()}";
    }
}
