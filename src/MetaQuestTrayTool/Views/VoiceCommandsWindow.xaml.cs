using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class VoiceCommandsWindow : Window
{
    private readonly ObservableCollection<VoicePhraseRow> _rows = [];
    private bool _loading;
    private bool _recordingPtt;
    private HotKeyModifiers _pttModifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift;
    private string _pttKey = "V";

    public VoiceCommandsWindow()
    {
        InitializeComponent();
        PhrasesList.ItemsSource = _rows;

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
        UpdatePushToTalkText();

        _rows.Clear();
        foreach (var phrase in VoicePhraseCatalog.Phrases)
        {
            _rows.Add(new VoicePhraseRow(phrase.Phrase, phrase.DescribeAction()));
        }

        StatusText.Text = App.Instance.Voice.Status + " Changes save automatically.";
        _loading = false;
    }

    private void UpdatePushToTalkText()
    {
        PushToTalkText.Text = new HotKeyBinding
        {
            Modifiers = _pttModifiers,
            Key = _pttKey
        }.DescribeChord();
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

    private void Save_Click(object sender, RoutedEventArgs e) => PersistIfValid();

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

    private sealed record VoicePhraseRow(string Phrase, string ActionLabel);
}
