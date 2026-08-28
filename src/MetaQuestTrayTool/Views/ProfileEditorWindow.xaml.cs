using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class ProfileEditorWindow : Window
{
    private static readonly string[] Priorities = ["Normal", "AboveNormal", "High"];

    public GameProfile Profile { get; }

    public ProfileEditorWindow(GameProfile profile)
    {
        InitializeComponent();
        Profile = profile;

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

        foreach (var priority in Priorities)
        {
            PriorityBox.Items.Add(priority);
        }

        PlatformBox.Items.Add(GamePlatform.Custom);
        PlatformBox.Items.Add(GamePlatform.Steam);
        PlatformBox.Items.Add(GamePlatform.Meta);

        SharpenBox.Items.Add(new ComboBoxItem { Content = "Inherit (use global)", Tag = LinkSharpeningMode.Default });
        SharpenBox.Items.Add(new ComboBoxItem { Content = "Off", Tag = LinkSharpeningMode.Disabled });
        SharpenBox.Items.Add(new ComboBoxItem { Content = "Normal", Tag = LinkSharpeningMode.Normal });
        SharpenBox.Items.Add(new ComboBoxItem { Content = "Quality", Tag = LinkSharpeningMode.Quality });

        BitrateBox.Items.Add(new ComboBoxItem { Content = "Inherit (use global)", Tag = null });
        foreach (var bitrate in LinkSettings.BitratePresets)
        {
            BitrateBox.Items.Add(new ComboBoxItem
            {
                Content = bitrate <= 0 ? "Meta default" : $"{bitrate} Mbps",
                Tag = bitrate
            });
        }

        OpenXrBox.Items.Add(new ComboBoxItem { Content = "Inherit (use global)", Tag = OpenXrRuntimeKind.Inherit });
        OpenXrBox.Items.Add(new ComboBoxItem { Content = "Meta / Oculus", Tag = OpenXrRuntimeKind.Meta });
        OpenXrBox.Items.Add(new ComboBoxItem { Content = "SteamVR", Tag = OpenXrRuntimeKind.SteamVr });

        EncodeWidthBox.Items.Add(new ComboBoxItem { Content = "Inherit (use global)", Tag = null });
        foreach (var width in LinkSettings.EncodeWidthPresets)
        {
            EncodeWidthBox.Items.Add(new ComboBoxItem
            {
                Content = width <= 0 ? "Auto / default" : width.ToString(),
                Tag = width
            });
        }

        NameBox.Text = profile.Name;
        ProcessBox.Text = profile.ProcessName;
        PlatformBox.SelectedItem = profile.Platform;
        MsfsVrBox.IsChecked = profile.ExperimentalMsfsVr;
        LaunchArgumentsBox.Text = profile.LaunchArguments
            ?? (profile.ExperimentalMsfsVr ? "-FastLaunch" : string.Empty);
        MsfsAutoToggleBox.IsChecked = profile.ExperimentalMsfsVrAutoToggle;
        MsfsDelayBox.Text = Math.Clamp(profile.ExperimentalMsfsVrToggleDelaySeconds, 5, 600)
            .ToString(CultureInfo.InvariantCulture);
        MsfsHotkeyBox.Text = string.IsNullOrWhiteSpace(profile.ExperimentalMsfsVrHotkey)
            ? "Ctrl+Tab"
            : profile.ExperimentalMsfsVrHotkey;
        MsfsVrPanel.IsEnabled = profile.ExperimentalMsfsVr;
        ScopeText.Text = profile.Scope == ProfileScope.Personal
            ? "Scope: Personal app profile (overrides global defaults while this process runs)."
            : "Scope: Global";
        FovBox.Text = profile.Settings.FovMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
        CommentsBox.Text = profile.Comments ?? string.Empty;
        CliCommandsBox.Text = profile.CustomCommands.ToCliText();
        AdbCommandsBox.Text = profile.CustomCommands.ToAdbText();
        SelectByTag(SuperSamplingBox, profile.Settings.SuperSampling);
        SelectByTag(AswBox, profile.Settings.AswMode);
        SelectByTag(SharpenBox, profile.Link.Sharpening);
        SelectNullableInt(BitrateBox, profile.Link.BitrateMbps);
        SelectNullableInt(EncodeWidthBox, profile.Link.EncodeResolutionWidth);
        SelectByTag(OpenXrBox, profile.OpenXrRuntime);
        PriorityBox.SelectedItem = Priorities.Contains(profile.CpuPriority) ? profile.CpuPriority : "Normal";
        LoadPresets(profile.ProcessName);
    }

    private void LoadPresets(string? processName)
    {
        PresetBox.Items.Clear();
        PresetBox.Items.Add(new ComboBoxItem { Content = "(none — keep current values)", Tag = null });
        foreach (var preset in ProfilePresetCatalog.PresetsForEditor(processName))
        {
            PresetBox.Items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag = preset,
                ToolTip = preset.Description
            });
        }

        PresetBox.SelectedIndex = 0;
        UpdatePresetHint(null);
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PresetBox.SelectedItem is not ComboBoxItem item)
        {
            UpdatePresetHint(null);
            return;
        }

        UpdatePresetHint(item.Tag as ProfilePreset);
    }

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not ComboBoxItem { Tag: ProfilePreset preset })
        {
            System.Windows.MessageBox.Show(this, "Choose a preset first.", App.AppName);
            return;
        }

        ProfilePresetCatalog.ApplyToProfile(Profile, preset);
        if (string.IsNullOrWhiteSpace(NameBox.Text) && preset.Kind == ProfilePresetKind.Game)
        {
            NameBox.Text = preset.Name;
        }

        if (string.IsNullOrWhiteSpace(ProcessBox.Text) && !string.IsNullOrWhiteSpace(preset.ProcessName))
        {
            ProcessBox.Text = preset.ProcessName;
        }

        FovBox.Text = Profile.Settings.FovMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
        CommentsBox.Text = Profile.Comments ?? string.Empty;
        CliCommandsBox.Text = Profile.CustomCommands.ToCliText();
        AdbCommandsBox.Text = Profile.CustomCommands.ToAdbText();
        SelectByTag(SuperSamplingBox, Profile.Settings.SuperSampling);
        SelectByTag(AswBox, Profile.Settings.AswMode);
        SelectByTag(SharpenBox, Profile.Link.Sharpening);
        SelectNullableInt(BitrateBox, Profile.Link.BitrateMbps);
        SelectNullableInt(EncodeWidthBox, Profile.Link.EncodeResolutionWidth);
        SelectByTag(OpenXrBox, Profile.OpenXrRuntime);
        PriorityBox.SelectedItem = Priorities.Contains(Profile.CpuPriority) ? Profile.CpuPriority : "Normal";
        UpdatePresetHint(preset);
    }

    private void UpdatePresetHint(ProfilePreset? preset)
    {
        PresetHintText.Text = preset is null
            ? "Pick a built-in PCVR preset (MSFS, Beat Saber, etc.) or a generic template, then Apply preset."
            : preset.Description;
    }

    private void MsfsVr_Changed(object sender, RoutedEventArgs e)
    {
        MsfsVrPanel.IsEnabled = MsfsVrBox.IsChecked == true;
        if (MsfsVrBox.IsChecked == true && string.IsNullOrWhiteSpace(LaunchArgumentsBox.Text))
        {
            LaunchArgumentsBox.Text = "-FastLaunch";
        }
    }

    private void BrowseLibrary_Click(object sender, RoutedEventArgs e)
    {
        var picker = new LibraryPickerWindow
        {
            Owner = this
        };
        if (picker.ShowDialog() != true || picker.SelectedGame is not { } game)
        {
            return;
        }

        NameBox.Text = game.Name;
        ProcessBox.Text = game.ProcessName;
        PlatformBox.SelectedItem = game.Platform;
        Profile.AppId = game.AppId;
        Profile.InstallPath = game.InstallPath;
        if (string.IsNullOrWhiteSpace(CommentsBox.Text))
        {
            CommentsBox.Text = $"{game.PlatformLabel} library import";
        }

        LoadPresets(game.ProcessName);
        var preset = ProfilePresetCatalog.BestGamePresetForProcess(game.ProcessName);
        if (preset is not null)
        {
            foreach (ComboBoxItem item in PresetBox.Items)
            {
                if (item.Tag is ProfilePreset candidate && candidate.Id == preset.Id)
                {
                    PresetBox.SelectedItem = item;
                    break;
                }
            }

            UpdatePresetHint(preset);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Give the profile a name.", App.AppName);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProcessBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Enter the game process name, for example BeatSaber.", App.AppName);
            return;
        }

        if (!double.TryParse(FovBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fov)
            || fov < 0.5 || fov > 1.5)
        {
            System.Windows.MessageBox.Show(this, "FOV multiplier must be a number between 0.50 and 1.50.", App.AppName);
            return;
        }

        Profile.Name = NameBox.Text.Trim();
        Profile.ProcessName = ProfileService.NormalizeProcessName(ProcessBox.Text);
        Profile.Platform = PlatformBox.SelectedItem is GamePlatform platform ? platform : GamePlatform.Custom;
        Profile.ExperimentalMsfsVr = MsfsVrBox.IsChecked == true;
        if (Profile.ExperimentalMsfsVr && !ExperimentalMsfsVrService.IsMsfs2024(Profile))
        {
            System.Windows.MessageBox.Show(
                this,
                "Experimental MSFS 2024 VR launch requires the process name FlightSimulator2024.",
                App.AppName);
            return;
        }

        Profile.LaunchArguments = string.IsNullOrWhiteSpace(LaunchArgumentsBox.Text)
            ? Profile.ExperimentalMsfsVr ? "-FastLaunch" : null
            : LaunchArgumentsBox.Text.Trim();
        Profile.ExperimentalMsfsVrAutoToggle = MsfsAutoToggleBox.IsChecked == true;
        var hasMsfsDelay = int.TryParse(
            MsfsDelayBox.Text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var msfsDelay);
        if (Profile.ExperimentalMsfsVr
            && (!hasMsfsDelay || msfsDelay is < 5 or > 600))
        {
            System.Windows.MessageBox.Show(
                this,
                "MSFS VR toggle delay must be between 5 and 600 seconds.",
                App.AppName);
            return;
        }

        Profile.ExperimentalMsfsVrToggleDelaySeconds = Profile.ExperimentalMsfsVr && hasMsfsDelay
            ? Math.Clamp(msfsDelay, 5, 600)
            : Math.Clamp(Profile.ExperimentalMsfsVrToggleDelaySeconds, 5, 600);
        Profile.ExperimentalMsfsVrHotkey = string.IsNullOrWhiteSpace(MsfsHotkeyBox.Text)
            ? "Ctrl+Tab"
            : MsfsHotkeyBox.Text.Trim();
        Profile.Scope = ProfileScope.Personal;
        Profile.Settings.SuperSampling = SuperSamplingBox.SelectedItem is ComboBoxItem ssItem && ssItem.Tag is double ss
            ? ss
            : 1.0;
        Profile.Settings.AswMode = AswBox.SelectedItem is ComboBoxItem aswItem && aswItem.Tag is AswMode mode
            ? mode
            : AswMode.Inherit;
        Profile.Settings.FovMultiplier = fov;
        Profile.Link.Sharpening = SharpenBox.SelectedItem is ComboBoxItem { Tag: LinkSharpeningMode sharpen }
            ? sharpen
            : LinkSharpeningMode.Default;
        Profile.Link.BitrateMbps = BitrateBox.SelectedItem is ComboBoxItem bitrateItem && bitrateItem.Tag is int bitrate
            ? bitrate
            : null;
        Profile.Link.EncodeResolutionWidth = EncodeWidthBox.SelectedItem is ComboBoxItem encodeItem && encodeItem.Tag is int width
            ? width
            : null;
        Profile.OpenXrRuntime = OpenXrBox.SelectedItem is ComboBoxItem { Tag: OpenXrRuntimeKind openXr }
            ? openXr
            : OpenXrRuntimeKind.Inherit;
        Profile.CpuPriority = PriorityBox.SelectedItem as string ?? "Normal";
        Profile.Comments = string.IsNullOrWhiteSpace(CommentsBox.Text) ? null : CommentsBox.Text.Trim();
        Profile.CustomCommands.SetCliFromText(CliCommandsBox.Text);
        Profile.CustomCommands.SetAdbFromText(AdbCommandsBox.Text);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

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

    private static void SelectNullableInt(System.Windows.Controls.ComboBox box, int? value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (value is null && item.Tag is null)
            {
                box.SelectedItem = item;
                return;
            }

            if (value is int selected && item.Tag is int tag && tag == selected)
            {
                box.SelectedItem = item;
                return;
            }
        }

        box.SelectedIndex = 0;
    }
}
