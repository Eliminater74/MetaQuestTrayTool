using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class AudioSettingsWindow : Window
{
    private bool _loading;

    public AudioSettingsWindow()
    {
        InitializeComponent();
        TriggerBox.Items.Add(new ComboBoxItem
        {
            Content = "Headset is Windows default output (recommended — Oculus virtual audio stays installed even when offline)",
            Tag = AudioSwitchTrigger.LinkAudioDevice
        });
        TriggerBox.Items.Add(new ComboBoxItem
        {
            Content = "Oculus service running (legacy — often wrong after Link ends)",
            Tag = AudioSwitchTrigger.OculusService
        });

        VrPlaybackBox.SelectionChanged += (_, _) => Persist_Changed();
        VrRecordingBox.SelectionChanged += (_, _) => Persist_Changed();
        FallbackPlaybackBox.SelectionChanged += (_, _) => Persist_Changed();
        FallbackRecordingBox.SelectionChanged += (_, _) => Persist_Changed();
        TriggerBox.SelectionChanged += (_, _) => Persist_Changed();
        AutoSwitchBox.Checked += (_, _) => Persist_Changed();
        AutoSwitchBox.Unchecked += (_, _) => Persist_Changed();
        CaptureEachSessionBox.Checked += (_, _) => Persist_Changed();
        CaptureEachSessionBox.Unchecked += (_, _) => Persist_Changed();
        CommunicationsBox.Checked += (_, _) => Persist_Changed();
        CommunicationsBox.Unchecked += (_, _) => Persist_Changed();

        ReloadDevices();
    }

    private void ReloadDevices()
    {
        _loading = true;
        var audio = App.Instance.Settings.Current.Audio;
        Populate(VrPlaybackBox, AudioDeviceKind.Playback, audio.VrPlaybackDeviceId);
        Populate(VrRecordingBox, AudioDeviceKind.Recording, audio.VrRecordingDeviceId);
        Populate(FallbackPlaybackBox, AudioDeviceKind.Playback, audio.FallbackPlaybackDeviceId);
        Populate(FallbackRecordingBox, AudioDeviceKind.Recording, audio.FallbackRecordingDeviceId);
        AutoSwitchBox.IsChecked = audio.AutoSwitchEnabled;
        CaptureEachSessionBox.IsChecked = audio.CaptureFallbackOnEachLinkSession;
        CommunicationsBox.IsChecked = audio.AlsoSetCommunicationsRole;
        SelectTrigger(audio.Trigger);
        StatusText.Text = App.Instance.Audio.DescribeLinkAudioState(audio)
                          + " Changes save automatically.";
        _loading = false;
    }

    private void Populate(System.Windows.Controls.ComboBox box, AudioDeviceKind kind, string? selectedId)
    {
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });

        foreach (var device in App.Instance.Audio.ListDevices(kind))
        {
            var marker = device.IsDefaultMultimedia ? "  [default]" : string.Empty;
            var headset = App.Instance.Audio.LooksLikeHeadset(device) ? "  [headset]" : string.Empty;
            box.Items.Add(new ComboBoxItem
            {
                Content = device.Name + marker + headset,
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

    private void SelectTrigger(AudioSwitchTrigger trigger)
    {
        foreach (ComboBoxItem item in TriggerBox.Items)
        {
            if (item.Tag is AudioSwitchTrigger value && value == trigger)
            {
                TriggerBox.SelectedItem = item;
                return;
            }
        }

        TriggerBox.SelectedIndex = 0;
    }

    private void Persist_Changed()
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        WriteToSettings();
        App.Instance.Settings.Save();
        App.Instance.Log.Info("Audio settings saved.");
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
        audio.CaptureFallbackOnEachLinkSession = CaptureEachSessionBox.IsChecked == true;
        audio.AlsoSetCommunicationsRole = CommunicationsBox.IsChecked == true;
        audio.Trigger = TriggerBox.SelectedItem is ComboBoxItem triggerItem && triggerItem.Tag is AudioSwitchTrigger trigger
            ? trigger
            : AudioSwitchTrigger.LinkAudioDevice;
        audio.VrPlaybackDeviceId = SelectedId(VrPlaybackBox);
        audio.VrRecordingDeviceId = SelectedId(VrRecordingBox);
        audio.FallbackPlaybackDeviceId = SelectedId(FallbackPlaybackBox);
        audio.FallbackRecordingDeviceId = SelectedId(FallbackRecordingBox);
    }

    private static string? SelectedId(System.Windows.Controls.ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Tag as string : null;
}
