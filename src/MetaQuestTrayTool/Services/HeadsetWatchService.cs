using System.Windows.Threading;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// ADB headset connect watcher. Meta's debug.oculus.* props do not survive reboot,
/// so we re-apply when the Quest appears — the set-and-forget path while you are in VR.
/// </summary>
public sealed class HeadsetWatchService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private string? _lastSerial;
    private bool _appliedForSerial;
    private string? _lastIgnoredMessage;
    private DateTime _lastWirelessAttemptUtc = DateTime.MinValue;
    private static readonly TimeSpan WirelessRetryInterval = TimeSpan.FromSeconds(45);
    private int _pollGate;

    public HeadsetWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _timer.Tick += (_, _) => BeginPoll();
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        _timer.Start();
        _app.Log.Info("Headset ADB watcher started.");
        BeginPoll();
    }

    public void Dispose() => _timer.Stop();

    private void BeginPoll()
    {
        if (Interlocked.Exchange(ref _pollGate, 1) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Warn($"Headset ADB: {ex.Message}"));
            }
            finally
            {
                Interlocked.Exchange(ref _pollGate, 0);
            }
        });
    }

    private void Poll()
    {
        MaybeAutoReconnectWireless();

        var quest = _app.Adb.FindQuest();
        var serial = quest?.IsReady == true ? quest.Serial : null;
        if (serial is null)
        {
            if (_lastSerial is not null)
            {
                var was = _lastSerial;
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Info($"ADB headset disconnected — was {was}."));
            }

            _lastSerial = null;
            _appliedForSerial = false;
            var ignored = _app.Adb.DescribeIgnoredDevices();
            if (ignored is not null && !string.Equals(ignored, _lastIgnoredMessage, StringComparison.Ordinal))
            {
                _lastIgnoredMessage = ignored;
                _app.Dispatcher.BeginInvoke(() => _app.Log.Info(ignored));
            }

            return;
        }

        _lastIgnoredMessage = null;

        var connected = !string.Equals(_lastSerial, serial, StringComparison.OrdinalIgnoreCase);
        _lastSerial = serial;
        if (connected)
        {
            var transport = AdbService.LooksLikeWirelessSerial(serial) ? "wireless" : "USB";
            var label = string.IsNullOrWhiteSpace(quest?.Model) ? serial : $"{quest!.Model} ({serial})";
            _app.Dispatcher.BeginInvoke(() =>
                _app.Log.Info($"ADB headset connected ({transport}) — {label}."));
        }

        if (!connected && _appliedForSerial)
        {
            return;
        }

        if (!_app.Settings.Current.Headset.ApplyWhenHeadsetConnects)
        {
            if (connected)
            {
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Info("ADB headset connect — auto-apply is off (enable under Headset settings)."));
            }

            return;
        }

        // Apply + settings save must stay on the UI/settings thread path.
        _app.Dispatcher.Invoke(() =>
        {
            try
            {
                var result = _app.Headset.Apply(_app.Settings.Current.Headset);
                _app.Settings.Save();
                _app.Log.Info($"Applied headset ADB settings — {result}");
                _appliedForSerial = true;

                if (!_app.IsGameProfileActive
                    && _app.Settings.Current.ApplyGlobalWhenHeadsetConnects)
                {
                    var global = _app.ApplyGlobalBaseline(notify: connected);
                    _app.Log.Info($"Applied global baseline on ADB connect — {global}");
                }

                if (connected)
                {
                    _app.TrayNotify("Headset", result);
                }
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Headset ADB: {ex.Message}");
            }
        });
    }

    private void MaybeAutoReconnectWireless()
    {
        var settings = _app.Settings.Current.Headset;
        if (!settings.WirelessAutoReconnect || settings.WirelessEndpoint is null)
        {
            return;
        }

        if (DateTime.UtcNow - _lastWirelessAttemptUtc < WirelessRetryInterval)
        {
            return;
        }

        // Skip if any ready VR headset is already listed (USB or wireless).
        if (_app.Adb.FindQuest()?.IsReady == true)
        {
            return;
        }

        _lastWirelessAttemptUtc = DateTime.UtcNow;
        var summary = _app.Adb.TryAutoReconnect(settings);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        if (summary.Contains("Connected", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Already connected", StringComparison.OrdinalIgnoreCase))
        {
            _app.Log.Info(summary);
        }
        else if (summary.Contains("auto-reconnect", StringComparison.OrdinalIgnoreCase))
        {
            // Soft fail — don't spam WARN every 30s while headset is off.
            _app.Log.Info(summary);
        }
    }
}
