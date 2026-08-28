using System.Diagnostics;
using System.Runtime.InteropServices;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Best-effort MSFS 2024 VR launch automation. MSFS still owns the actual 2D-to-VR
/// transition, so this service only prepares the selected PCVR path and sends a
/// user-configured toggle after the simulator window has had time to load.
/// </summary>
public sealed class ExperimentalMsfsVrService : IDisposable
{
    private const string MsfsProcessName = "FlightSimulator2024";
    private readonly App _app;
    private readonly object _gate = new();
    private CancellationTokenSource? _toggleCts;

    public ExperimentalMsfsVrService(App app) => _app = app;

    public string Prepare(GameProfile profile)
    {
        if (!IsMsfs2024(profile))
        {
            return "Experimental MSFS VR launch ignored — use it only with FlightSimulator2024.";
        }

        var setup = PcvrSetup.GetMode(_app);
        var targetRuntime = profile.OpenXrRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr
            ? profile.OpenXrRuntime
            : PcvrSetup.ExpectedOpenXr(_app);
        var useSteamVr = setup == PcvrSetupMode.SteamVrOverMetaLink
                         || targetRuntime == OpenXrRuntimeKind.SteamVr;
        var result = useSteamVr
            ? _app.DashToSteamVr.StartSteamVrNow("experimental MSFS 2024 VR")
            : _app.Oculus.ShowMetaHorizonLink();
        var summary = useSteamVr
            ? "SteamVR over Meta Link prepared. " + result
            : "Meta Horizon Link prepared. " + result;
        _app.Log.Info(summary);
        return summary;
    }

    public void ScheduleToggle(GameProfile profile)
    {
        if (!profile.ExperimentalMsfsVr
            || !IsMsfs2024(profile)
            || !profile.ExperimentalMsfsVrAutoToggle)
        {
            return;
        }

        CancellationTokenSource cts;
        lock (_gate)
        {
            _toggleCts?.Cancel();
            _toggleCts?.Dispose();
            _toggleCts = new CancellationTokenSource();
            cts = _toggleCts;
        }

        var delay = Math.Clamp(profile.ExperimentalMsfsVrToggleDelaySeconds, 5, 600);
        var hotkey = profile.ExperimentalMsfsVrHotkey;
        _ = Task.Run(async () =>
        {
            try
            {
                var window = await WaitForMainWindowAsync(
                    profile.ProcessName,
                    TimeSpan.FromSeconds(120),
                    cts.Token).ConfigureAwait(false);
                if (window is null)
                {
                    _ = _app.Dispatcher.BeginInvoke(() =>
                        _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                            "MSFS window was not found. VR toggle was not sent."));
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(delay), cts.Token).ConfigureAwait(false);
                if (!IsProcessRunning(profile.ProcessName))
                {
                    _ = _app.Dispatcher.BeginInvoke(() =>
                        _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                            "MSFS closed before the VR toggle. No key was sent."));
                    return;
                }

                if (!TryParseHotkey(hotkey, out var modifier, out var key))
                {
                    _ = _app.Dispatcher.BeginInvoke(() =>
                        _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                            "The configured MSFS VR toggle is unsupported. Use Ctrl plus Tab, Shift plus Tab, or Alt plus Tab."));
                    return;
                }

                if (!SetForegroundWindow(window.Value))
                {
                    _ = _app.Dispatcher.BeginInvoke(() =>
                        _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                            "MSFS was found, but its window could not be focused. VR toggle was not sent."));
                    return;
                }

                await Task.Delay(250, cts.Token).ConfigureAwait(false);
                if (!SendHotkey(modifier, key))
                {
                    _ = _app.Dispatcher.BeginInvoke(() =>
                        _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                            "MSFS was focused, but the VR toggle could not be sent."));
                    return;
                }

                _ = _app.Dispatcher.BeginInvoke(() =>
                    _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                        $"Sent {DescribeHotkey(hotkey)} to MSFS. Verify that the simulator entered VR."));
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // A newer launch or application shutdown replaced this pending toggle.
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Experimental MSFS VR toggle failed: {ex.Message}");
                _ = _app.Dispatcher.BeginInvoke(() =>
                    _app.HeadsetAnnouncer.AnnounceExperimentalMsfsVr(
                        "Experimental MSFS VR toggle failed. Check Log."));
            }
        }, cts.Token);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _toggleCts?.Cancel();
            _toggleCts?.Dispose();
            _toggleCts = null;
        }
    }

    public static bool IsMsfs2024(GameProfile profile) =>
        ProfileService.NormalizeProcessName(profile.ProcessName)
            .Equals(MsfsProcessName, StringComparison.OrdinalIgnoreCase);

    private static async Task<nint?> WaitForMainWindowAsync(
        string processName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var normalized = ProfileService.NormalizeProcessName(processName);
        if (normalized.Length == 0)
        {
            return null;
        }

        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            foreach (var process in Process.GetProcessesByName(normalized))
            {
                using (process)
                {
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        return process.MainWindowHandle;
                    }
                }
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(ProfileService.NormalizeProcessName(processName));
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseHotkey(string? value, out ushort modifier, out ushort key)
    {
        modifier = 0;
        key = 0;
        var normalized = (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
        var parts = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[1].Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        modifier = parts[0].ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => 0x11,
            "SHIFT" => 0x10,
            "ALT" => 0x12,
            _ => (ushort)0
        };
        key = modifier == 0 ? (ushort)0 : (ushort)0x09;
        return key != 0;
    }

    private static string DescribeHotkey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "the configured VR toggle" : value.Trim();

    private static bool SendHotkey(ushort modifier, ushort key)
    {
        var inputs = new[]
        {
            KeyInput(modifier, keyUp: false),
            KeyInput(key, keyUp: false),
            KeyInput(key, keyUp: true),
            KeyInput(modifier, keyUp: true)
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) == inputs.Length;
    }

    private static NativeInput KeyInput(ushort key, bool keyUp) => new()
    {
        Type = 1,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = key,
                Flags = keyUp ? 0x0002u : 0u
            }
        }
    };

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, NativeInput[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
