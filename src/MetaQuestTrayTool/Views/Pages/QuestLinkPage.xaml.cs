using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views.Pages;

public partial class QuestLinkPage : System.Windows.Controls.UserControl, IShellPage
{
    public QuestLinkPage()
    {
        InitializeComponent();

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
    }

    public void Refresh()
    {
        var link = App.Instance.Settings.Current.LinkSettings;
        SelectByTag(EncodeWidthBox, link.EncodeResolutionWidth);
        SelectByTag(BitrateBox, link.BitrateMbps);
        SelectByTag(DynamicBox, link.EncodeDynamicBitrate);
        SelectByTag(DynamicMaxBox, link.DynamicBitrateMax);
        SelectByTag(SharpenBox, link.Sharpening);
        SelectByTag(DistortionBox, link.DistortionCurvature);
        HevcBox.IsChecked = link.PreferHevc;
        SlicesBox.IsChecked = link.DisableSlicedEncoding;
        ApplyOnStartBox.IsChecked = App.Instance.Settings.Current.ApplyLinkSettingsOnStart;
        LiveStatusText.Text = "Live: " + App.Instance.Link.ReadCurrent().Describe();
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
        var result = App.Instance.Link.Apply(App.Instance.Settings.Current.LinkSettings, deleteUnsetOverrides: true);
        if (result.Succeeded)
        {
            App.Instance.Log.Info(result.Summary);
        }
        else
        {
            App.Instance.Log.Error(result.Summary);
        }

        if (restart)
        {
            var serviceResult = App.Instance.Oculus.Restart();
            App.Instance.Log.Info(serviceResult);
        }

        LiveStatusText.Text = result.Summary;
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

        if (SharpenBox.SelectedItem is ComboBoxItem { Tag: LinkSharpeningMode sharpen })
        {
            link.Sharpening = sharpen;
        }

        if (DistortionBox.SelectedItem is ComboBoxItem { Tag: DistortionCurvature distortion })
        {
            link.DistortionCurvature = distortion;
        }

        link.PreferHevc = HevcBox.IsChecked == true;
        link.DisableSlicedEncoding = SlicesBox.IsChecked == true;
        App.Instance.Settings.Current.ApplyLinkSettingsOnStart = ApplyOnStartBox.IsChecked == true;
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
