using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class QuestLinkPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public QuestLinkPage()
    {
        InitializeComponent();

        foreach (var preset in LinkPresetCatalog.All)
        {
            PresetBox.Items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag = preset
            });
        }

        if (PresetBox.Items.Count > 0)
        {
            PresetBox.SelectedIndex = 0;
        }

        foreach (var width in LinkSettings.EncodeWidthPresets)
        {
            EncodeWidthBox.Items.Add(new ComboBoxItem
            {
                Content = width <= 0 ? "Default / auto" : width.ToString(),
                Tag = width
            });
        }

        foreach (var bitrate in LinkSettings.BitratePresets)
        {
            BitrateBox.Items.Add(new ComboBoxItem
            {
                Content = bitrate <= 0 ? "Default" : $"{bitrate} Mbps",
                Tag = bitrate
            });
            DynamicMaxBox.Items.Add(new ComboBoxItem
            {
                Content = bitrate <= 0 ? "0 (unused)" : $"{bitrate} Mbps",
                Tag = bitrate
            });
        }

        DynamicOffsetBox.Items.Add(new ComboBoxItem { Content = "0 (none)", Tag = 0 });
        foreach (var offset in new[] { 25, 50, 75, 100, 150, 200 })
        {
            DynamicOffsetBox.Items.Add(new ComboBoxItem
            {
                Content = $"+{offset} Mbps",
                Tag = offset
            });
        }

        foreach (EncodeDynamicBitrateMode mode in Enum.GetValues<EncodeDynamicBitrateMode>())
        {
            DynamicBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }

        foreach (LinkSharpeningMode mode in Enum.GetValues<LinkSharpeningMode>())
        {
            SharpenBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }

        foreach (DistortionCurvature mode in Enum.GetValues<DistortionCurvature>())
        {
            DistortionBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }

        foreach (MobileAswMode mode in Enum.GetValues<MobileAswMode>())
        {
            MobileAswBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }
    }

    public void Refresh()
    {
        _loading = true;
        var link = App.Instance.Settings.Current.LinkSettings;
        SelectMatchingPreset(link);
        LoadFieldsFrom(link);
        ApplyOnStartBox.IsChecked = App.Instance.Settings.Current.ApplyLinkSettingsOnStart;
        LiveStatusText.Text = "Live: " + App.Instance.Link.ReadCurrent().Describe();

        var caps = App.Instance.LinkConnection.GetCapabilities();
        MetaLinkPanel.IsEnabled = caps.AllowsMetaLinkRegistry;
        PresetBox.IsEnabled = caps.AllowsMetaLinkRegistry;
        if (string.IsNullOrWhiteSpace(caps.Banner) || caps.AllowsMetaLinkRegistry)
        {
            SessionBanner.Visibility = Visibility.Collapsed;
            SessionBanner.Text = string.Empty;
        }
        else
        {
            SessionBanner.Visibility = Visibility.Visible;
            SessionBanner.Text = caps.Banner;
        }

        _loading = false;
        UpdatePresetHint();
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        UpdatePresetHint();
    }

    private void ApplyPresetFields_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not ComboBoxItem { Tag: LinkPreset preset })
        {
            return;
        }

        _loading = true;
        LoadFieldsFrom(preset.Settings);
        _loading = false;
        LiveStatusText.Text = $"Loaded preset “{preset.Name}” into fields — click Save to write the registry.";
        App.Instance.Log.Info($"Quest Link preset loaded into fields: {preset.Name}.");
    }

    private void ApplyPresetSave_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not ComboBoxItem { Tag: LinkPreset preset })
        {
            return;
        }

        _loading = true;
        LoadFieldsFrom(preset.Settings);
        _loading = false;
        Apply(restart: false);
        LiveStatusText.Text = $"Applied preset “{preset.Name}”. " + LiveStatusText.Text;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Apply(restart: false);

    private void SaveRestart_Click(object sender, RoutedEventArgs e) => Apply(restart: true);

    private void ReadLive_Click(object sender, RoutedEventArgs e)
    {
        LiveStatusText.Text = "Live: " + App.Instance.Link.ReadCurrent().Describe();
    }

    private void Apply(bool restart)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var summary = App.Instance.ApplyMetaLinkSettings(App.Instance.Settings.Current.LinkSettings, deleteUnsetOverrides: true);

        if (restart && App.Instance.LinkConnection.GetCapabilities().AllowsMetaLinkRegistry)
        {
            var serviceResult = App.Instance.Oculus.Restart();
            App.Instance.Log.Info(serviceResult);
            LiveStatusText.Text = summary + " " + serviceResult;
        }
        else
        {
            LiveStatusText.Text = summary;
        }
    }

    private void LoadFieldsFrom(LinkSettings link)
    {
        SelectByTag(EncodeWidthBox, link.EncodeResolutionWidth);
        SelectByTag(BitrateBox, link.BitrateMbps);
        SelectByTag(DynamicBox, link.EncodeDynamicBitrate);
        SelectByTag(DynamicMaxBox, link.DynamicBitrateMax);
        SelectByTag(DynamicOffsetBox, link.DynamicBitrateOffsetMbps);
        SelectByTag(SharpenBox, link.Sharpening);
        SelectByTag(DistortionBox, link.DistortionCurvature);
        SelectByTag(MobileAswBox, link.MobileAsw);
        HevcBox.IsChecked = link.PreferHevc;
        SlicesBox.IsChecked = link.DisableSlicedEncoding;
    }

    private void WriteToSettings()
    {
        var link = App.Instance.Settings.Current.LinkSettings;
        if (EncodeWidthBox.SelectedItem is ComboBoxItem { Tag: int width })
        {
            link.EncodeResolutionWidth = width;
        }

        if (BitrateBox.SelectedItem is ComboBoxItem { Tag: int bitrate })
        {
            link.BitrateMbps = bitrate;
        }

        if (DynamicBox.SelectedItem is ComboBoxItem { Tag: EncodeDynamicBitrateMode dynamic })
        {
            link.EncodeDynamicBitrate = dynamic;
        }

        if (DynamicMaxBox.SelectedItem is ComboBoxItem { Tag: int max })
        {
            link.DynamicBitrateMax = max;
        }

        if (DynamicOffsetBox.SelectedItem is ComboBoxItem { Tag: int offset })
        {
            link.DynamicBitrateOffsetMbps = offset;
        }

        if (SharpenBox.SelectedItem is ComboBoxItem { Tag: LinkSharpeningMode sharpen })
        {
            link.Sharpening = sharpen;
        }

        if (DistortionBox.SelectedItem is ComboBoxItem { Tag: DistortionCurvature distortion })
        {
            link.DistortionCurvature = distortion;
        }

        if (MobileAswBox.SelectedItem is ComboBoxItem { Tag: MobileAswMode mobileAsw })
        {
            link.MobileAsw = mobileAsw;
        }

        link.PreferHevc = HevcBox.IsChecked == true;
        link.DisableSlicedEncoding = SlicesBox.IsChecked == true;
        if (PresetBox.SelectedItem is ComboBoxItem { Tag: LinkPreset preset })
        {
            link.PresetName = preset.Name;
        }
        else
        {
            link.PresetName = "Custom";
        }

        App.Instance.Settings.Current.ApplyLinkSettingsOnStart = ApplyOnStartBox.IsChecked == true;
    }

    private void SelectMatchingPreset(LinkSettings link)
    {
        foreach (ComboBoxItem item in PresetBox.Items)
        {
            if (item.Tag is LinkPreset preset
                && string.Equals(preset.Name, link.PresetName, StringComparison.OrdinalIgnoreCase))
            {
                PresetBox.SelectedItem = item;
                return;
            }
        }

        // Leave current selection; fields still load from settings.
    }

    private void UpdatePresetHint()
    {
        if (PresetBox.SelectedItem is ComboBoxItem { Tag: LinkPreset preset })
        {
            PresetHintText.Text = preset.Description;
        }
        else
        {
            PresetHintText.Text = string.Empty;
        }
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag))
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
