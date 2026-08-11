using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views.Pages;

public partial class GameSettingsPage : System.Windows.Controls.UserControl, IShellPage
{
    public GameSettingsPage()
    {
        InitializeComponent();

        foreach (var value in GameSettings.SuperSamplingPresets)
        {
            SuperSamplingBox.Items.Add(new ComboBoxItem
            {
                Content = value <= 0 ? "Off (no override)" : value.ToString("0.0"),
                Tag = value
            });
        }

        foreach (AswMode mode in Enum.GetValues<AswMode>())
        {
            AswBox.Items.Add(new ComboBoxItem { Content = FormatAsw(mode), Tag = mode });
        }

        AdaptiveGpuBox.Items.Add(new ComboBoxItem { Content = "On", Tag = true });
        AdaptiveGpuBox.Items.Add(new ComboBoxItem { Content = "Off", Tag = false });

        foreach (var priority in new[] { "Idle", "BelowNormal", "Normal", "AboveNormal", "High", "Realtime" })
        {
            PriorityBox.Items.Add(new ComboBoxItem { Content = priority, Tag = priority });
        }

        ForceMipBox.Items.Add(new ComboBoxItem { Content = "False", Tag = false });
        ForceMipBox.Items.Add(new ComboBoxItem { Content = "True", Tag = true });

        foreach (var offset in new[] { 0.0, -1.0, -2.0, 1.0, 2.0 })
        {
            OffsetMipBox.Items.Add(new ComboBoxItem { Content = offset.ToString("0"), Tag = offset });
        }

        SuperSamplingBox.SelectionChanged += (_, _) => PersistIfReady();
        AswBox.SelectionChanged += (_, _) => PersistIfReady();
        AdaptiveGpuBox.SelectionChanged += (_, _) => PersistIfReady();
        PriorityBox.SelectionChanged += (_, _) => PersistIfReady();
        ForceMipBox.SelectionChanged += (_, _) => PersistIfReady();
        OffsetMipBox.SelectionChanged += (_, _) => PersistIfReady();
        ApplyOnStartBox.Checked += (_, _) => PersistFlags();
        ApplyOnStartBox.Unchecked += (_, _) => PersistFlags();
        AutoApplyBox.Checked += (_, _) => PersistFlags();
        AutoApplyBox.Unchecked += (_, _) => PersistFlags();
    }

    public void Refresh()
    {
        var settings = App.Instance.Settings.Current;
        var game = settings.DefaultGameSettings;
        SelectByTag(SuperSamplingBox, game.SuperSampling);
        SelectByTag(AswBox, game.AswMode);
        SelectByTag(AdaptiveGpuBox, game.AdaptiveGpuScaling);
        SelectByTag(PriorityBox, game.OvrServerPriority);
        SelectByTag(ForceMipBox, game.ForceMipMapOnLayers);
        SelectByTag(OffsetMipBox, game.OffsetMipMapOnLayers);
        FovHBox.Text = game.FovMultiplierHorizontal.ToString("0.00", CultureInfo.InvariantCulture);
        FovVBox.Text = game.FovMultiplierVertical.ToString("0.00", CultureInfo.InvariantCulture);
        ApplyOnStartBox.IsChecked = settings.ApplyGameSettingsOnStart;
        AutoApplyBox.IsChecked = settings.AutoApplyProfiles;
        StatusText.Text = App.Instance.DebugTool.IsAvailable
            ? $"CLI: {App.Instance.DebugTool.CliPath}"
            : "OculusDebugToolCLI not found. Install Meta Quest PC software.";
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProfilesWindow { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void GlobalDefaults_Click(object sender, RoutedEventArgs e)
    {
        var window = new GlobalDefaultsWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            Refresh();
        }
    }

    private void SaveFov_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteFov())
        {
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved FOV multipliers.");
        StatusText.Text = "FOV saved.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteAll())
        {
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved game setting defaults.");
        StatusText.Text = "Defaults saved.";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteAll())
        {
            return;
        }

        App.Instance.Settings.Save();
        var result = App.Instance.DebugTool.Apply(App.Instance.Settings.Current.DefaultGameSettings);
        if (!result.CliFound || !result.Started)
        {
            App.Instance.Log.Error(result.Summary);
        }
        else if (result.LooksRejected)
        {
            App.Instance.Log.Warn(result.Summary);
        }
        else
        {
            App.Instance.Log.Info(result.Summary);
        }

        StatusText.Text = result.Summary;
    }

    private void PersistIfReady()
    {
        if (!IsLoaded)
        {
            return;
        }

        TryWriteAll(showErrors: false);
        App.Instance.Settings.Save();
    }

    private void PersistFlags()
    {
        if (!IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current;
        settings.ApplyGameSettingsOnStart = ApplyOnStartBox.IsChecked == true;
        settings.AutoApplyProfiles = AutoApplyBox.IsChecked == true;
        App.Instance.Settings.Save();
    }

    private bool TryWriteAll(bool showErrors = true)
    {
        if (!TryWriteFov(showErrors))
        {
            return false;
        }

        var game = App.Instance.Settings.Current.DefaultGameSettings;
        if (SuperSamplingBox.SelectedItem is ComboBoxItem { Tag: double ss })
        {
            game.SuperSampling = ss;
        }

        if (AswBox.SelectedItem is ComboBoxItem { Tag: AswMode asw })
        {
            game.AswMode = asw;
        }

        if (AdaptiveGpuBox.SelectedItem is ComboBoxItem { Tag: bool adaptive })
        {
            game.AdaptiveGpuScaling = adaptive;
        }

        if (PriorityBox.SelectedItem is ComboBoxItem { Tag: string priority })
        {
            game.OvrServerPriority = priority;
        }

        if (ForceMipBox.SelectedItem is ComboBoxItem { Tag: bool forceMip })
        {
            game.ForceMipMapOnLayers = forceMip;
        }

        if (OffsetMipBox.SelectedItem is ComboBoxItem { Tag: double offset })
        {
            game.OffsetMipMapOnLayers = offset;
        }

        PersistFlags();
        return true;
    }

    private bool TryWriteFov(bool showErrors = true)
    {
        if (!double.TryParse(FovHBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || !double.TryParse(FovVBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            || h < 0.5 || h > 1.5 || v < 0.5 || v > 1.5)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(
                    Window.GetWindow(this),
                    "FOV multipliers must be between 0.50 and 1.50.",
                    App.AppName);
            }

            return false;
        }

        var game = App.Instance.Settings.Current.DefaultGameSettings;
        game.FovMultiplierHorizontal = h;
        game.FovMultiplierVertical = v;
        return true;
    }

    private static string FormatAsw(AswMode mode) => mode switch
    {
        AswMode.Inherit => "Inherit",
        AswMode.Off => "Off",
        AswMode.Auto => "Auto",
        AswMode.Clock45 => "45 FPS",
        _ => mode.ToString()
    };

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object? tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag)
                || (item.Tag is double a && tag is double b && Math.Abs(a - b) < 0.001)
                || (item.Tag is string s && tag is string t && string.Equals(s, t, StringComparison.OrdinalIgnoreCase)))
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }
}
