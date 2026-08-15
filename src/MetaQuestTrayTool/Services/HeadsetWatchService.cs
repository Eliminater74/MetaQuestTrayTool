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
    private string? _lastSweepMessage;
    private DateTime _lastWirelessAttemptUtc = DateTime.MinValue;
    private static readonly TimeSpan WirelessRetryInterval = TimeSpan.FromSeconds(45);
    private int _pollGate;

    public HeadsetWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = IdleCadence.HeavyIdle };
        _timer.Tick += (_, _) => BeginPoll();
    }

    public void Start() => SyncWatch();

    public void Stop() => _timer.Stop();

    public void Dispose() => Stop();

    /// <summary>Stop ADB polling when apply-on-connect, wireless auto-reconnect, and headset-only sweep are all off.</summary>
    public void SyncWatch()
    {
        var settings = _app.Settings.Current.Headset;
        var needsWatch = settings.ApplyWhenHeadsetConnects
                         || settings.WirelessAutoReconnect
                         || settings.HeadsetOnlyWirelessAdb;
        if (!needsWatch)
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _app.Log.Info("Headset ADB watcher paused (auto-apply, wireless reconnect, and headset-only sweep off).");
            }

            return;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _app.Log.Info("Headset ADB watcher started.");
            BeginPoll();
        }

        ApplyCadence(_lastSerial is not null);
    }

    private void ApplyCadence(bool headsetPresent)
    {
        IdleCadence.Set(_timer, headsetPresent ? IdleCadence.Watching : IdleCadence.HeavyIdle);
    }

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
        var settings = _app.Settings.Current.Headset;
        if (!settings.ApplyWhenHeadsetConnects
            && !settings.WirelessAutoReconnect
            && !settings.HeadsetOnlyWirelessAdb)
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

        MaybeAutoReconnectWireless();
        MaybeSweepNonHeadsetWireless();

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

            _app.Dispatcher.BeginInvoke(() => ApplyCadence(headsetPresent: false));
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
            _app.Dispatcher.BeginInvoke(() => ApplyCadence(headsetPresent: true));
            return;
        }

        if (!_app.Settings.Current.Headset.ApplyWhenHeadsetConnects)
        {
            if (connected)
            {
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Info("ADB headset connect — auto-apply is off (enable under Headset settings)."));
            }

            _app.Dispatcher.BeginInvoke(() => ApplyCadence(headsetPresent: true));
            return;
        }

        try
        {
            var result = _app.Headset.Apply(_app.Settings.Current.Headset);
            string? global = null;
            if (!_app.IsGameProfileActive
                && _app.Settings.Current.ApplyGlobalWhenHeadsetConnects)
            {
                global = _app.ApplyGlobalBaseline(notify: connected);
            }

            _appliedForSerial = true;
            _app.Dispatcher.BeginInvoke(() =>
            {
                _app.Settings.Save();
                _app.Log.Info($"Applied headset ADB settings — {result}");
                if (!string.IsNullOrWhiteSpace(global))
                {
                    _app.Log.Info($"Applied global baseline on ADB connect — {global}");
                }

                if (connected)
                {
                    _app.TrayNotify("Headset", result);
                }

                ApplyCadence(headsetPresent: true);
            });
        }
        catch (Exception ex)
        {
            _app.Dispatcher.BeginInvoke(() =>
            {
                _app.Log.Warn($"Headset ADB: {ex.Message}");
                ApplyCadence(headsetPresent: true);
            });
        }
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
        else if (summary.Contains("auto-reconnect", StringComparison.OrdinalIgnoreCase)
                 || summary.Contains("not a VR headset", StringComparison.OrdinalIgnoreCase))
        {
            // Soft fail — don't spam WARN every 30s while headset is off or a phone is on the LAN.
            _app.Log.Info(summary);
        }
    }

    private void MaybeSweepNonHeadsetWireless()
    {
        var settings = _app.Settings.Current.Headset;
        if (!settings.HeadsetOnlyWirelessAdb)
        {
            return;
        }

        var summary = _app.Adb.SweepNonHeadsetWireless(settings);
        if (string.IsNullOrWhiteSpace(summary)
            || string.Equals(summary, _lastSweepMessage, StringComparison.Ordinal))
        {
            return;
        }

        _lastSweepMessage = summary;
        _app.Log.Info(summary);
    }
}
