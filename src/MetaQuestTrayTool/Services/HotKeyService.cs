using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Registers global hotkeys through a hidden message window.
/// </summary>
public sealed class HotKeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly App _app;
    private readonly HotKeyCommandService _commands;
    private HotKeyMessageWindow? _window;
    private readonly Dictionary<int, HotKeyBinding> _registered = new();

    public HotKeyService(App app, HotKeyCommandService commands)
    {
        _app = app;
        _commands = commands;
    }

    public void Reload()
    {
        UnregisterAll();
        var settings = _app.Settings.Current.HotKeys;
        if (!settings.Enabled)
        {
            return;
        }

        settings.EnsureBindingIds();
        EnsureWindow();

        if (HotKeyChordHelper.ConflictsWithHotKeys(_app.Settings.Current.Voice, settings))
        {
            _app.Log.Warn("Hotkeys may conflict with voice push-to-talk shortcut — change one of them in Configure.");
        }

        foreach (var binding in settings.Bindings)
        {
            TryRegister(binding);
        }

        if (_registered.Count > 0)
        {
            _app.Log.Info($"Hotkeys active ({_registered.Count} binding(s)).");
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_window is not null)
        {
            _window.HotKeyPressed -= OnHotKeyPressed;
            _window.Dispose();
            _window = null;
        }
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new HotKeyMessageWindow();
        _window.HotKeyPressed += OnHotKeyPressed;
        _window.CreateControl();
    }

    private void OnHotKeyPressed(int id)
    {
        if (!_registered.TryGetValue(id, out var binding))
        {
            return;
        }

        _app.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var summary = _commands.Execute(binding.Action, HotKeyCommandSource.HotKey);
                _app.Log.Info($"Hotkey {binding.DescribeChord()} → {binding.DescribeAction()}: {summary}");
                if (_app.Settings.Current.ShowNotifications)
                {
                    _app.TrayNotify("Hotkey", $"{binding.DescribeAction()}\n{summary}");
                }
            }
            catch (Exception ex)
            {
                _app.Log.Error($"Hotkey {binding.DescribeChord()} failed.", ex);
            }
        });
    }

    private void TryRegister(HotKeyBinding binding)
    {
        if (_window is null || !binding.TryParseKey(out var key))
        {
            return;
        }

        var modifiers = ToNativeModifiers(binding.Modifiers) | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            _app.Log.Warn($"Hotkey skipped — could not map key {binding.Key}.");
            return;
        }

        if (!RegisterHotKey(_window.Handle, binding.Id, modifiers, virtualKey))
        {
            _app.Log.Warn($"Hotkey not registered (in use?): {binding.DescribeChord()} → {binding.DescribeAction()}");
            return;
        }

        _registered[binding.Id] = binding;
    }

    private void UnregisterAll()
    {
        if (_window is null || _registered.Count == 0)
        {
            _registered.Clear();
            return;
        }

        foreach (var id in _registered.Keys.ToList())
        {
            UnregisterHotKey(_window.Handle, id);
        }

        _registered.Clear();
    }

    private static uint ToNativeModifiers(HotKeyModifiers modifiers)
    {
        uint native = 0;
        if (modifiers.HasFlag(HotKeyModifiers.Alt))
        {
            native |= 0x0001;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Control))
        {
            native |= 0x0002;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Shift))
        {
            native |= 0x0004;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Windows))
        {
            native |= 0x0008;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class HotKeyMessageWindow : Form
    {
        public event Action<int>? HotKeyPressed;

        public HotKeyMessageWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Size = new System.Drawing.Size(0, 0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotKey)
            {
                HotKeyPressed?.Invoke(m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }
    }
}
