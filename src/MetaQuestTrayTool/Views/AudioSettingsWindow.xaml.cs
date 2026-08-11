using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class AudioSettingsWindow : Window
{
    public AudioSettingsWindow()
    {
        InitializeComponent();
        ReloadDevices();
    }

    private void ReloadDevices()
    {
        var audio = App.Instance.Settings.Current.Audio;
        Populate(VrPlaybackBox, AudioDeviceKind.Playback, audio.VrPlaybackDeviceId);
        Populate(VrRecordingBox, AudioDeviceKind.Recording, audio.VrRecordingDeviceId);
        Populate(FallbackPlaybackBox, AudioDeviceKind.Playback, audio.FallbackPlaybackDeviceId);
        Populate(FallbackRecordingBox, AudioDeviceKind.Recording, audio.FallbackRecordingDeviceId);
        AutoSwitchBox.IsChecked = audio.AutoSwitchEnabled;
        CommunicationsBox.IsChecked = audio.AlsoSetCommunicationsRole;
    }

    private void Populate(System.Windows.Controls.ComboBox box, AudioDeviceKind kind, string? selectedId)
    {
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });

        foreach (var device in App.Instance.Audio.ListDevices(kind))
        {
            var marker = device.IsDefaultMultimedia ? "  [default]" : string.Empty;
            box.Items.Add(new ComboBoxItem
            {
                Content = device.Name + marker,
                Tag = device.Id
            });
        }

        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, selectedId) || (item.Tag is null && string.IsNullOrWhiteSpace(selectedId)))
            {
                box.SelectedItem = item;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved audio switching settings.");
        System.Windows.MessageBox.Show(this, "Audio settings saved.", App.AppName);
    }

    private void SwitchVr_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var result = App.Instance.Audio.ApplyVrDevices(App.Instance.Settings.Current.Audio);
        App.Instance.Log.Info(result);
        ReloadDevices();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var result = App.Instance.Audio.RestoreFallbackDevices(App.Instance.Settings.Current.Audio);
        App.Instance.Log.Info(result);
        ReloadDevices();
    }

    private void CaptureFallback_Click(object sender, RoutedEventArgs e)
    {
        var audio = App.Instance.Settings.Current.Audio;
        var result = App.Instance.Audio.CaptureCurrentAsFallback(audio);
        App.Instance.Settings.Save();
        App.Instance.Log.Info(result);
        ReloadDevices();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void WriteToSettings()
    {
        var audio = App.Instance.Settings.Current.Audio;
        audio.AutoSwitchEnabled = AutoSwitchBox.IsChecked == true;
        audio.AlsoSetCommunicationsRole = CommunicationsBox.IsChecked == true;
        audio.VrPlaybackDeviceId = SelectedId(VrPlaybackBox);
        audio.VrRecordingDeviceId = SelectedId(VrRecordingBox);
        audio.FallbackPlaybackDeviceId = SelectedId(FallbackPlaybackBox);
        audio.FallbackRecordingDeviceId = SelectedId(FallbackRecordingBox);
    }

    private static string? SelectedId(System.Windows.Controls.ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Tag as string : null;
}
