using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class GlobalDefaultsWindow : Window
{
    public GlobalDefaultsWindow()
    {
        InitializeComponent();

        foreach (var preset in ProfilePresetCatalog.GlobalPresets)
        {
            PresetBox.Items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag = preset,
                ToolTip = preset.Description
            });
        }

        if (PresetBox.Items.Count > 0)
        {
            PresetBox.SelectedIndex = 0;
            UpdatePresetHint(ProfilePresetCatalog.GlobalPresets[0]);
        }

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
            AswBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }

        OpenXrBox.Items.Add(new ComboBoxItem { Content = "Meta / Oculus", Tag = OpenXrRuntimeKind.Meta });
        OpenXrBox.Items.Add(new ComboBoxItem { Content = "SteamVR", Tag = OpenXrRuntimeKind.SteamVr });

        var defaults = App.Instance.Settings.Current.DefaultGameSettings;
        SelectByTag(SuperSamplingBox, defaults.SuperSampling);
        SelectByTag(AswBox, defaults.AswMode);
        SelectByTag(OpenXrBox, App.Instance.Settings.Current.OpenXr.PreferredRuntime == OpenXrRuntimeKind.Inherit
            ? OpenXrRuntimeKind.Meta
            : App.Instance.Settings.Current.OpenXr.PreferredRuntime);
        FovBox.Text = defaults.FovMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
        CliCommandsBox.Text = App.Instance.Settings.Current.CustomCommands.ToCliText();
        AdbCommandsBox.Text = App.Instance.Settings.Current.CustomCommands.ToAdbText();
    }

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not ComboBoxItem { Tag: ProfilePreset preset })
        {
            return;
        }

        ProfilePresetCatalog.ApplyGlobalPreset(App.Instance.Settings.Current, preset);
        SelectByTag(SuperSamplingBox, App.Instance.Settings.Current.DefaultGameSettings.SuperSampling);
        SelectByTag(AswBox, App.Instance.Settings.Current.DefaultGameSettings.AswMode);
        SelectByTag(OpenXrBox, App.Instance.Settings.Current.OpenXr.PreferredRuntime == OpenXrRuntimeKind.Inherit
            ? OpenXrRuntimeKind.Meta
            : App.Instance.Settings.Current.OpenXr.PreferredRuntime);
        FovBox.Text = App.Instance.Settings.Current.DefaultGameSettings.FovMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
        UpdatePresetHint(preset);
    }

    private void UpdatePresetHint(ProfilePreset preset) =>
        PresetHintText.Text = preset.Description;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWrite(out _))
        {
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved global default game settings.");
        DialogResult = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWrite(out _))
        {
            return;
        }

        App.Instance.Settings.Save();
        var summary = App.Instance.ApplyGlobalGameSettings();
        App.Instance.Log.Info("Applied global defaults: " + summary);
        System.Windows.MessageBox.Show(this, summary, App.AppName);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool TryWrite(out GameSettings settings)
    {
        settings = App.Instance.Settings.Current.DefaultGameSettings;
        if (!double.TryParse(FovBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fov)
            || fov < 0.5 || fov > 1.5)
        {
            System.Windows.MessageBox.Show(this, "FOV multiplier must be between 0.50 and 1.50.", App.AppName);
            return false;
        }

        settings.SuperSampling = SuperSamplingBox.SelectedItem is ComboBoxItem ssItem && ssItem.Tag is double ss
            ? ss
            : 1.0;
        settings.AswMode = AswBox.SelectedItem is ComboBoxItem aswItem && aswItem.Tag is AswMode mode
            ? mode
            : AswMode.Auto;
        settings.FovMultiplier = fov;
        if (OpenXrBox.SelectedItem is ComboBoxItem { Tag: OpenXrRuntimeKind openXr }
            && openXr is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            App.Instance.Settings.Current.OpenXr.PreferredRuntime = openXr;
        }

        App.Instance.Settings.Current.CustomCommands.SetCliFromText(CliCommandsBox.Text);
        App.Instance.Settings.Current.CustomCommands.SetAdbFromText(AdbCommandsBox.Text);
        return true;
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (item.Tag is double left && value is double right && Math.Abs(left - right) < 0.001)
            {
                box.SelectedItem = item;
                return;
            }

            if (Equals(item.Tag, value))
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
