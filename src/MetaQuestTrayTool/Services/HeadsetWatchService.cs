using System.Windows.Threading;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// ADB headset connect watcher. Meta's debug.oculus.* props do not survive reboot,
/// so we re-apply when the Quest appears — the set-and-forget path while you are in VR.
/// </summary>
public sealed class HeadsetWatchService : IDisposable
{
    public static readonly TimeSpan DefaultTimedPause = TimeSpan.FromHours(2);

    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _resumeTimer;
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
        _resumeTimer = new DispatcherTimer();
        _resumeTimer.Tick += (_, _) => OnResumeTimerTick();
    }

    public void Start() => SyncWatch();

    public void Stop()
    {
        _timer.Stop();
        _resumeTimer.Stop();
    }

    public void Dispose() => Stop();

    /// <summary>
    /// True while the ADB watcher is paused (indefinite or until a deadline).
    /// Does not expire the pause — only <see cref="SyncWatch"/> does, so the poll timer restarts.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            var settings = _app.Settings.Current.Headset;
            if (!settings.AdbWatcherPaused)
            {
                return false;
            }

            if (settings.AdbWatcherPausedUntilUtc is { } until && DateTime.UtcNow >= until)
            {
                // Deadline passed but SyncWatch has not run yet — schedule resume on the UI thread.
                _app.Dispatcher.BeginInvoke(SyncWatch);
                return false;
            }

            return true;
        }
    }

    /// <summary>Human status for tray / Status page (empty when not paused).</summary>
    public string PauseStatusText
    {
        get
        {
            if (!IsPaused)
            {
                return string.Empty;
            }

            var until = _app.Settings.Current.Headset.AdbWatcherPausedUntilUtc;
            if (until is null)
            {
                return "ADB paused until you resume (other devices safe).";
            }

            var local = until.Value.ToLocalTime();
            return $"ADB paused until {local:t} ({local:MMM d}) — other devices safe.";
        }
    }

    /// <summary>
    /// Stop polling / auto-reconnect / headset-only disconnect so phones, TVs, etc. can use ADB.
    /// Pass <paramref name="duration"/> for a timed pause (e.g. 2 hours); null = until Resume.
    /// </summary>
    public void Pause(TimeSpan? duration = null, bool notify = true)
    {
        var settings = _app.Settings.Current.Headset;
        settings.AdbWatcherPaused = true;
        settings.AdbWatcherPausedUntilUtc = duration is { } d && d > TimeSpan.Zero
            ? DateTime.UtcNow.Add(d)
            : null;
        _app.Settings.Save();
        SyncWatch();

        var message = settings.AdbWatcherPausedUntilUtc is { } until
            ? $"ADB paused until {until.ToLocalTime():t}. Phones / TVs can use ADB without this tray disconnecting them."
            : "ADB paused until you resume. Phones / TVs can use ADB without this tray disconnecting them.";
        _app.Log.Info(message);
        if (notify)
        {
            _app.TrayNotify("ADB paused", message);
        }
    }

    /// <summary>Resume headset ADB watching (apply / reconnect / headset-only sweep).</summary>
    public void Resume(bool notify = true)
    {
        var settings = _app.Settings.Current.Headset;
        if (!settings.AdbWatcherPaused && settings.AdbWatcherPausedUntilUtc is null)
        {
            return;
        }

        settings.AdbWatcherPaused = false;
        settings.AdbWatcherPausedUntilUtc = null;
        _app.Settings.Save();
        SyncWatch();
        _app.Log.Info("Headset ADB watcher resumed.");
        if (notify)
        {
            _app.TrayNotify("ADB resumed", "Headset ADB watching is on again.");
        }
    }

    /// <summary>Stop ADB polling when paused, or when apply-on-connect, wireless auto-reconnect, and headset-only sweep are all off.</summary>
    public void SyncWatch()
    {
        var timedPauseEnded = ExpireTimedPauseIfNeeded();
        ArmResumeTimer();

        var settings = _app.Settings.Current.Headset;
        if (settings.AdbWatcherPaused)
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _app.Log.Info("Headset ADB watcher paused — will not poll, reconnect, or disconnect other devices.");
            }

            _app.RefreshTrayUi();
            return;
        }

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

            _app.RefreshTrayUi();
            return;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _app.Log.Info(timedPauseEnded
                ? "Headset ADB watcher resumed after timed pause."
                : "Headset ADB watcher started.");
            BeginPoll();
        }

        ApplyCadence(_lastSerial is not null);
        _app.RefreshTrayUi();
    }

    /// <summary>Clears an expired timed pause. Returns true when pause flags were cleared.</summary>
    private bool ExpireTimedPauseIfNeeded()
    {
        var settings = _app.Settings.Current.Headset;
        if (!settings.AdbWatcherPaused || settings.AdbWatcherPausedUntilUtc is not { } until)
        {
            return false;
        }

        if (DateTime.UtcNow < until)
        {
            return false;
        }

        settings.AdbWatcherPaused = false;
        settings.AdbWatcherPausedUntilUtc = null;
        _app.Settings.Save();
        _app.Log.Info("Timed ADB pause ended — headset watcher will resume.");
        _app.TrayNotify("ADB resumed", "Timed pause ended. Headset ADB watching is on again.");
        return true;
    }

    private void ArmResumeTimer()
    {
        _resumeTimer.Stop();
        var settings = _app.Settings.Current.Headset;
        if (!settings.AdbWatcherPaused || settings.AdbWatcherPausedUntilUtc is not { } until)
        {
            return;
        }

        var remaining = until - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        // DispatcherTimer max practical interval is fine for a few hours.
        _resumeTimer.Interval = remaining > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : remaining;
        _resumeTimer.Start();
    }

    private void OnResumeTimerTick()
    {
        _resumeTimer.Stop();
        SyncWatch();
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
        if (settings.AdbWatcherPaused
            || (!settings.ApplyWhenHeadsetConnects
                && !settings.WirelessAutoReconnect
                && !settings.HeadsetOnlyWirelessAdb))
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

        // Re-check pause after the gate — Pause may have landed while this poll was queued.
        if (_app.Settings.Current.Headset.AdbWatcherPaused)
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

        MaybeAutoReconnectWireless();

        if (_app.Settings.Current.Headset.AdbWatcherPaused)
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

        MaybeSweepNonHeadsetWireless();

        if (_app.Settings.Current.Headset.AdbWatcherPaused)
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

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
        if (settings.AdbWatcherPaused
            || !settings.WirelessAutoReconnect
            || settings.WirelessEndpoint is null)
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
        if (!settings.HeadsetOnlyWirelessAdb || settings.AdbWatcherPaused)
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
