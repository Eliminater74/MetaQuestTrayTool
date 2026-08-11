using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class LinkSettingsWindow : Window
{
    public LinkSettingsWindow()
    {
        InitializeComponent();

        foreach (var bitrate in LinkSettings.BitratePresets)
        {
            BitrateBox.Items.Add(new ComboBoxItem
            {
                Content = bitrate <= 0 ? "Default (no override)" : $"{bitrate} Mbps",
                Tag = bitrate
            });
        }

        foreach (var width in LinkSettings.EncodeWidthPresets)
        {
            EncodeWidthBox.Items.Add(new ComboBoxItem
            {
                Content = width <= 0 ? "Auto / default (0)" : width.ToString(),
                Tag = width
            });
        }

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var settings = App.Instance.Settings.Current.LinkSettings;
        SelectByTag(BitrateBox, settings.BitrateMbps);
        SelectByTag(EncodeWidthBox, settings.EncodeResolutionWidth);
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

    private LinkSettings ReadUi() => new()
    {
        BitrateMbps = BitrateBox.SelectedItem is ComboBoxItem bitrateItem && bitrateItem.Tag is int bitrate
            ? bitrate
            : 0,
        EncodeResolutionWidth = EncodeWidthBox.SelectedItem is ComboBoxItem widthItem && widthItem.Tag is int width
            ? width
            : 0,
        PreferHevc = HevcBox.IsChecked == true,
        DisableSlicedEncoding = SlicesBox.IsChecked == true
    };

    private static void SelectByTag(System.Windows.Controls.ComboBox box, int value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (item.Tag is int tag && tag == value)
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
