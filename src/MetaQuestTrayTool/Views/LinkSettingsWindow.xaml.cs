using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class LinkSettingsWindow : Window
{
    public LinkSettingsWindow()
    {
        InitializeComponent();

        foreach (var width in LinkSettings.EncodeWidthPresets)
        {
            EncodeWidthBox.Items.Add(new ComboBoxItem
            {
                Content = width <= 0 ? "Auto / default (0)" : width.ToString(),
                Tag = width
            });
        }

        foreach (var bitrate in LinkSettings.BitratePresets)
        {
            BitrateBox.Items.Add(new ComboBoxItem
            {
                Content = bitrate <= 0 ? "Default (no override)" : $"{bitrate} Mbps",
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

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var settings = App.Instance.Settings.Current.LinkSettings;
        SelectByTag(EncodeWidthBox, settings.EncodeResolutionWidth);
        SelectByTag(BitrateBox, settings.BitrateMbps);
        SelectByTag(DynamicBox, settings.EncodeDynamicBitrate);
        SelectByTag(DynamicMaxBox, settings.DynamicBitrateMax);
        SelectByTag(DynamicOffsetBox, settings.DynamicBitrateOffsetMbps);
        SelectByTag(SharpenBox, settings.Sharpening);
        SelectByTag(DistortionBox, settings.DistortionCurvature);
        SelectByTag(MobileAswBox, settings.MobileAsw);
        HevcBox.IsChecked = settings.PreferHevc;
        SlicesBox.IsChecked = settings.DisableSlicedEncoding;
        ApplyOnStartBox.IsChecked = App.Instance.Settings.Current.ApplyLinkSettingsOnStart;
        LiveStatusText.Text = "Live registry: " + App.Instance.Link.ReadCurrent().Describe();
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => Apply(restartService: false);

    private void ApplyRestart_Click(object sender, RoutedEventArgs e) => Apply(restartService: true);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Apply(bool restartService)
    {
        var settings = ReadUi();
        App.Instance.Settings.Current.LinkSettings = settings;
        App.Instance.Settings.Current.ApplyLinkSettingsOnStart = ApplyOnStartBox.IsChecked == true;
        App.Instance.Settings.Save();

        var result = App.Instance.Link.Apply(settings, deleteUnsetOverrides: true);
        if (result.Succeeded)
        {
            App.Instance.Log.Info(result.Summary);
        }
        else
        {
            App.Instance.Log.Error(result.Summary);
        }

        if (restartService)
        {
            var serviceResult = App.Instance.Oculus.Restart();
            App.Instance.Log.Info(serviceResult);
        }

        LiveStatusText.Text = "Live registry: " + App.Instance.Link.ReadCurrent().Describe();
    }

    private LinkSettings ReadUi()
    {
        var settings = App.Instance.Settings.Current.LinkSettings.Clone();
        settings.EncodeResolutionWidth = EncodeWidthBox.SelectedItem is ComboBoxItem widthItem && widthItem.Tag is int width
            ? width
            : 0;
        settings.BitrateMbps = BitrateBox.SelectedItem is ComboBoxItem bitrateItem && bitrateItem.Tag is int bitrate
            ? bitrate
            : 0;
        settings.EncodeDynamicBitrate = DynamicBox.SelectedItem is ComboBoxItem { Tag: EncodeDynamicBitrateMode dynamic }
            ? dynamic
            : EncodeDynamicBitrateMode.Default;
        settings.DynamicBitrateMax = DynamicMaxBox.SelectedItem is ComboBoxItem maxItem && maxItem.Tag is int max
            ? max
            : 0;
        settings.DynamicBitrateOffsetMbps = DynamicOffsetBox.SelectedItem is ComboBoxItem offsetItem && offsetItem.Tag is int offset
            ? offset
            : 0;
        settings.Sharpening = SharpenBox.SelectedItem is ComboBoxItem { Tag: LinkSharpeningMode sharpen }
            ? sharpen
            : LinkSharpeningMode.Default;
        settings.DistortionCurvature = DistortionBox.SelectedItem is ComboBoxItem { Tag: DistortionCurvature distortion }
            ? distortion
            : DistortionCurvature.Default;
        settings.MobileAsw = MobileAswBox.SelectedItem is ComboBoxItem { Tag: MobileAswMode mobileAsw }
            ? mobileAsw
            : MobileAswMode.Default;
        settings.PreferHevc = HevcBox.IsChecked == true;
        settings.DisableSlicedEncoding = SlicesBox.IsChecked == true;
        return settings;
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
