using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class VoiceCommandsWindow : Window
{
    private readonly ObservableCollection<VoicePhraseRow> _rows = [];
    private readonly ObservableCollection<VoicePhraseRow> _customRows = [];
    private bool _loading;
    private bool _recordingPtt;
    private HotKeyModifiers _pttModifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift;
    private string _pttKey = "V";

    public VoiceCommandsWindow()
    {
        InitializeComponent();
        PhrasesList.ItemsSource = _rows;
        CustomPhrasesList.ItemsSource = _customRows;

        foreach (var action in HotKeyCatalog.AllActions)
        {
            CustomActionBox.Items.Add(new ComboBoxItem
            {
                Content = HotKeyCatalog.DescribeAction(action),
                Tag = action
            });
        }

        if (CustomActionBox.Items.Count > 0)
        {
            CustomActionBox.SelectedIndex = 0;
        }

        EnabledBox.Checked += (_, _) => PersistIfValid();
        EnabledBox.Unchecked += (_, _) => PersistIfValid();
        PushToTalkBox.Checked += (_, _) => PersistIfValid();
        PushToTalkBox.Unchecked += (_, _) => PersistIfValid();
        AudioConfirmBox.Checked += (_, _) => PersistIfValid();
        AudioConfirmBox.Unchecked += (_, _) => PersistIfValid();

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _loading = true;
        var voice = App.Instance.Settings.Current.Voice;
        EnabledBox.IsChecked = voice.Enabled;
        PushToTalkBox.IsChecked = voice.PushToTalkOnly;
        AudioConfirmBox.IsChecked = voice.AudioConfirmation;
        _pttModifiers = voice.PushToTalkModifiers;
        _pttKey = voice.PushToTalkKey;
        ConfidenceSlider.Value = Math.Clamp(voice.MinConfidence, 0.35f, 0.90f);
        ConfidenceValueText.Text = ConfidenceSlider.Value.ToString("P0", CultureInfo.CurrentCulture);
        UpdatePushToTalkText();
        LoadMicBox(voice.PreferredRecordingDeviceId);

        _rows.Clear();
        foreach (var phrase in VoicePhraseCatalog.Phrases)
        {
            _rows.Add(new VoicePhraseRow(phrase.Phrase, phrase.DescribeAction()));
        }

        _customRows.Clear();
        foreach (var phrase in voice.NormalizedCustomPhrases())
        {
            _customRows.Add(new VoicePhraseRow(phrase.Phrase, HotKeyCatalog.DescribeAction(phrase.Action), phrase.Action));
        }

        StatusText.Text = App.Instance.Voice.Status + " Changes save automatically.";
        _loading = false;
    }

    private void LoadMicBox(string? preferredId)
    {
        MicBox.Items.Clear();
        MicBox.Items.Add(new ComboBoxItem { Content = "Windows default microphone", Tag = string.Empty });
        foreach (var device in App.Instance.Audio.ListDevices(AudioDeviceKind.Recording, force: true))
        {
            MicBox.Items.Add(new ComboBoxItem { Content = device.Name, Tag = device.Id });
        }

        var match = MicBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, preferredId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        MicBox.SelectedItem = match ?? MicBox.Items[0];
    }

    private void UpdatePushToTalkText()
    {
        PushToTalkText.Text = new HotKeyBinding
        {
            Modifiers = _pttModifiers,
            Key = _pttKey
        }.DescribeChord();
    }

    private void Mic_Changed(object sender, SelectionChangedEventArgs e) => PersistIfValid();

    private void Confidence_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConfidenceValueText is null)
        {
            return;
        }

        ConfidenceValueText.Text = e.NewValue.ToString("P0", CultureInfo.CurrentCulture);
        PersistIfValid();
    }

    private void AddCustom_Click(object sender, RoutedEventArgs e)
    {
        var phrase = (CustomPhraseBox.Text ?? string.Empty).Trim();
        if (phrase.Length == 0)
        {
            StatusText.Text = "Enter a custom phrase first.";
            return;
        }

        if (CustomActionBox.SelectedItem is not ComboBoxItem { Tag: HotKeyAction action })
        {
            StatusText.Text = "Pick an action for the custom phrase.";
            return;
        }

        if (_customRows.Any(row => row.Phrase.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text = "That custom phrase already exists.";
            return;
        }

        _customRows.Add(new VoicePhraseRow(phrase, HotKeyCatalog.DescribeAction(action), action));
        CustomPhraseBox.Text = string.Empty;
        PersistIfValid();
    }

    private void RemoveCustom_Click(object sender, RoutedEventArgs e)
    {
        if (CustomPhrasesList.SelectedItem is not VoicePhraseRow row)
        {
            StatusText.Text = "Select a custom phrase to remove.";
            return;
        }

        _customRows.Remove(row);
        PersistIfValid();
    }

    private void RecordPtt_Click(object sender, RoutedEventArgs e)
    {
        _recordingPtt = true;
        RecordPttButton.Content = "Listening…";
        StatusText.Text = "Press the push-to-talk shortcut (Esc cancels).";
        Focus();
    }

    private void ResetPtt_Click(object sender, RoutedEventArgs e)
    {
        _pttModifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift;
        _pttKey = "V";
        UpdatePushToTalkText();
        PersistIfValid();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recordingPtt)
        {
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            _recordingPtt = false;
            RecordPttButton.Content = "Record shortcut…";
            StatusText.Text = App.Instance.Voice.Status;
            e.Handled = true;
            return;
        }

        if (IsModifierKey(e.Key))
        {
            return;
        }

        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key) || key == System.Windows.Input.Key.None)
        {
            return;
        }

        _pttModifiers = ReadModifiers();
        _pttKey = key.ToString();
        UpdatePushToTalkText();
        _recordingPtt = false;
        RecordPttButton.Content = "Record shortcut…";
        e.Handled = true;
        PersistIfValid();
    }

    private void TestListen_Click(object sender, RoutedEventArgs e)
    {
        WriteUiToSettings();
        App.Instance.Voice.Reload();
        App.Instance.Voice.ListenOnce();
        StatusText.Text = "Testing one-shot listen — speak a phrase from the list.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void PersistIfValid()
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        WriteUiToSettings();
        var voice = App.Instance.Settings.Current.Voice;
        if (voice.Enabled
            && App.Instance.Settings.Current.HotKeys.Enabled
            && HotKeyChordHelper.ConflictsWithHotKeys(voice, App.Instance.Settings.Current.HotKeys))
        {
            StatusText.Text = "Push-to-talk conflicts with a hotkey — change one shortcut.";
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Voice.Reload();
        StatusText.Text = App.Instance.Voice.Status;
        App.Instance.Log.Info(StatusText.Text);
    }

    private void WriteUiToSettings()
    {
        var voice = App.Instance.Settings.Current.Voice;
        voice.Enabled = EnabledBox.IsChecked == true;
        voice.PushToTalkOnly = PushToTalkBox.IsChecked == true;
        voice.AudioConfirmation = AudioConfirmBox.IsChecked == true;
        voice.PushToTalkModifiers = _pttModifiers;
        voice.PushToTalkKey = _pttKey;
        voice.MinConfidence = (float)ConfidenceSlider.Value;
        voice.PreferredRecordingDeviceId = MicBox.SelectedItem is ComboBoxItem { Tag: string id } && id.Length > 0
            ? id
            : null;
        voice.CustomPhrases = _customRows
            .Where(row => row.Action is HotKeyAction action)
            .Select(row => new VoiceCustomPhrase { Phrase = row.Phrase, Action = row.Action!.Value })
            .ToList();
    }

    private static HotKeyModifiers ReadModifiers()
    {
        var modifiers = Keyboard.Modifiers;
        var value = HotKeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= HotKeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= HotKeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= HotKeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= HotKeyModifiers.Windows;
        }

        return value;
    }

    private static bool IsModifierKey(System.Windows.Input.Key key) =>
        key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin;

    private sealed record VoicePhraseRow(string Phrase, string ActionLabel, HotKeyAction? Action = null);
}
