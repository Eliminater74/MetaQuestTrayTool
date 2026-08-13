using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class HotKeysWindow : Window
{
    private readonly ObservableCollection<HotKeyBindingRow> _rows = [];
    private HotKeyBindingRow? _selectedRow;
    private HotKeyBindingRow? _recordingRow;
    private bool _recording;

    public HotKeysWindow()
    {
        InitializeComponent();
        foreach (var action in HotKeyCatalog.AllActions)
        {
            ActionBox.Items.Add(new ComboBoxItem
            {
                Content = HotKeyCatalog.DescribeAction(action),
                Tag = action
            });
        }

        BindingsList.ItemsSource = _rows;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var hotKeys = App.Instance.Settings.Current.HotKeys;
        EnabledBox.IsChecked = hotKeys.Enabled;
        _rows.Clear();
        foreach (var binding in hotKeys.Bindings)
        {
            _rows.Add(HotKeyBindingRow.From(binding));
        }

        StatusText.Text = hotKeys.Enabled
            ? $"{_rows.Count} binding(s). Defaults use Ctrl + Numpad 0–9."
            : "HotKeys are off. Enable them to register shortcuts globally.";
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        PersistIfValid();
    }

    private void BindingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedRow = BindingsList.SelectedItem as HotKeyBindingRow;
        RemoveButton.IsEnabled = _selectedRow is not null;
        if (_selectedRow is not null && !_recording)
        {
            ShowRecordPanel(_selectedRow);
        }
    }

    private void ActionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedRow is null || ActionBox.SelectedItem is not ComboBoxItem { Tag: HotKeyAction action })
        {
            return;
        }

        _selectedRow.Action = action;
        _selectedRow.RefreshLabels();
        PersistIfValid();
    }

    private void AddBinding_Click(object sender, RoutedEventArgs e)
    {
        var nextId = _rows.Count == 0 ? 1 : _rows.Max(row => row.Id) + 1;
        var binding = new HotKeyBinding
        {
            Id = nextId,
            Action = HotKeyAction.AswCycle,
            Modifiers = HotKeyModifiers.Control,
            Key = "NumPad0"
        };
        var row = HotKeyBindingRow.From(binding);
        _rows.Add(row);
        BindingsList.SelectedItem = row;
        PersistIfValid();
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRow is null)
        {
            return;
        }

        _rows.Remove(_selectedRow);
        _selectedRow = null;
        RemoveButton.IsEnabled = false;
        RecordPanel.Visibility = Visibility.Collapsed;
        PersistIfValid();
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRow is null)
        {
            return;
        }

        StartRecording(_selectedRow);
    }

    private void CancelRecord_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
    }

    private void StartRecording(HotKeyBindingRow row)
    {
        _recordingRow = row;
        _recording = true;
        RecordPanel.Visibility = Visibility.Visible;
        RecordHintText.Text = "Listening… press a key combo (e.g. Ctrl+NumPad1). Esc cancels.";
        RecordButton.Content = "Listening…";
        Focus();
    }

    private void StopRecording()
    {
        _recording = false;
        _recordingRow = null;
        RecordButton.Content = "Record…";
        if (_selectedRow is not null)
        {
            RecordHintText.Text = $"Current: {_selectedRow.ChordLabel}. Click Record to change.";
        }
    }

    private void ShowRecordPanel(HotKeyBindingRow row)
    {
        RecordPanel.Visibility = Visibility.Visible;
        RecordHintText.Text = $"Current: {row.ChordLabel}. Click Record to change.";
        SelectAction(row.Action);
    }

    private void SelectAction(HotKeyAction action)
    {
        foreach (ComboBoxItem item in ActionBox.Items)
        {
            if (item.Tag is HotKeyAction value && value == action)
            {
                ActionBox.SelectedItem = item;
                return;
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recording || _recordingRow is null)
        {
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            StopRecording();
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

        _recordingRow.Modifiers = ReadModifiers();
        _recordingRow.Key = key.ToString();
        _recordingRow.RefreshLabels();
        e.Handled = true;
        StopRecording();
        PersistIfValid();
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        foreach (var binding in HotKeySettings.CreateDefaultBindings())
        {
            _rows.Add(HotKeyBindingRow.From(binding));
        }

        StatusText.Text = "Defaults restored.";
        PersistIfValid();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => PersistIfValid();

    private void PersistIfValid()
    {
        if (!IsLoaded)
        {
            return;
        }

        var bindings = _rows.Select(row => row.ToBinding()).ToList();
        if (HotKeyChordHelper.TryFindDuplicate(bindings, out var duplicate))
        {
            StatusText.Text = $"Duplicate shortcut: {duplicate!.DescribeChord()} — fix before it saves.";
            return;
        }

        if (EnabledBox.IsChecked == true
            && HotKeyChordHelper.ConflictsWithHotKeys(App.Instance.Settings.Current.Voice, new HotKeySettings
            {
                Enabled = true,
                Bindings = bindings
            }))
        {
            StatusText.Text = "Shortcut conflicts with voice push-to-talk — change one combo.";
            return;
        }

        var settings = App.Instance.Settings.Current;
        settings.HotKeys.Enabled = EnabledBox.IsChecked == true;
        settings.Tray.EnableHotKeys = settings.HotKeys.Enabled;
        settings.HotKeys.Bindings = bindings;
        settings.HotKeys.EnsureBindingIds();
        App.Instance.Settings.Save();
        App.Instance.HotKeys.Reload();
        StatusText.Text = settings.HotKeys.Enabled
            ? "Saved. HotKeys are active."
            : "Saved. HotKeys are disabled.";
        App.Instance.Log.Info(StatusText.Text);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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

    private sealed class HotKeyBindingRow : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public HotKeyAction Action { get; set; }
        public HotKeyModifiers Modifiers { get; set; }
        public string Key { get; set; } = "None";
        public string ActionLabel { get; private set; } = string.Empty;
        public string ChordLabel { get; private set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static HotKeyBindingRow From(HotKeyBinding binding)
        {
            var row = new HotKeyBindingRow
            {
                Id = binding.Id,
                Action = binding.Action,
                Modifiers = binding.Modifiers,
                Key = binding.Key
            };
            row.RefreshLabels();
            return row;
        }

        public HotKeyBinding ToBinding() => new()
        {
            Id = Id,
            Action = Action,
            Modifiers = Modifiers,
            Key = Key
        };

        public void RefreshLabels()
        {
            ActionLabel = HotKeyCatalog.DescribeAction(Action);
            ChordLabel = ToBinding().DescribeChord();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChordLabel)));
        }
    }
}
