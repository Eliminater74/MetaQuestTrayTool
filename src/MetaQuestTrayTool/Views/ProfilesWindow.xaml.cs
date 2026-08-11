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

    private void AddFromLibrary_Click(object sender, RoutedEventArgs e)
    {
        var picker = new LibraryPickerWindow
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedGame is not { } game)
        {
            return;
        }

        var existing = App.Instance.Profiles.FindByProcess(game.ProcessName);
        if (existing is not null)
        {
            var overwrite = System.Windows.MessageBox.Show(
                this,
                $"A personal profile for '{existing.ProcessName}' already exists ({existing.Name}). Open it?",
                App.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (overwrite == MessageBoxResult.Yes)
            {
                Edit(existing);
                App.Instance.Profiles.Save();
                Reload();
            }

            return;
        }

        var profile = new GameProfile
        {
            Name = game.Name,
            ProcessName = game.ProcessName,
            Platform = game.Platform,
            Scope = ProfileScope.Personal,
            AppId = game.AppId,
            InstallPath = game.InstallPath,
            Settings = App.Instance.Settings.Current.DefaultGameSettings.Clone(),
            Comments = $"{game.PlatformLabel} library import"
        };

        if (Edit(profile) == true)
        {
            App.Instance.Profiles.Add(profile);
            App.Instance.Log.Info($"Created personal profile '{profile.Name}' from {game.PlatformLabel}.");
            Reload();
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var profile = new GameProfile
        {
            Scope = ProfileScope.Personal,
            Platform = GamePlatform.Custom,
            Settings = App.Instance.Settings.Current.DefaultGameSettings.Clone()
        };

        if (Edit(profile) == true)
        {
            App.Instance.Profiles.Add(profile);
            App.Instance.Log.Info($"Created custom personal profile '{profile.Name}' for {profile.ProcessName}.");
            Reload();
        }
    }

    private void GlobalDefaults_Click(object sender, RoutedEventArgs e)
    {
        var window = new GlobalDefaultsWindow
        {
            Owner = this
        };
        window.ShowDialog();
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
            $"Delete personal profile '{profile.Name}'?",
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

        var summary = App.Instance.ApplyProfile(profile);
        App.Instance.Log.Info($"Applied personal profile '{profile.Name}': {summary}");
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

        public override string ToString()
        {
            var platform = Profile.Platform switch
            {
                GamePlatform.Steam => "Steam",
                GamePlatform.Meta => "Meta",
                _ => "Custom"
            };
            return $"{Profile.Name}  ·  {platform}  ·  {Profile.ProcessName}  ·  {Profile.Settings.Describe()}  ·  {Profile.Link.Describe()}";
        }
    }
}
