using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Applies Quest ADB settings after a confirmed PCVR session. It is not an always-on ADB detector.
/// Link detection stays in <see cref="LinkSessionWatchService"/> and does not use ADB.
/// </summary>
public sealed class HeadsetWatchService : IDisposable
{
    public static readonly TimeSpan DefaultTimedPause = TimeSpan.FromHours(2);

    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _resumeTimer;
    private readonly AdbSessionGate _gate = new();
    private readonly object _stateLock = new();
    private string? _lastSerial;
    private bool _appliedForSerial;
    private string? _lastIgnoredMessage;
    private DateTime _lastWirelessAttemptUtc = DateTime.MinValue;
    private static readonly TimeSpan WirelessRetryInterval = TimeSpan.FromSeconds(45);
    private int _pollGate;
    private int _manualDepth;

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
        if (!_app.Dispatcher.CheckAccess())
        {
            _app.Dispatcher.BeginInvoke(Stop);
            return;
        }

        lock (_stateLock)
        {
            _gate.NotifyEnded();
            ClearConnectionState();
        }

        _timer.Stop();
        _resumeTimer.Stop();
    }

    public void Dispose() => Stop();

    /// <summary>Called when Link session detection confirms a live PCVR stream. Does not probe Link itself.</summary>
    public void NotifyPcvrSession(VrConnectionStatus status)
    {
        if (!_app.Dispatcher.CheckAccess())
        {
            _app.Dispatcher.BeginInvoke(() => NotifyPcvrSession(status));
            return;
        }

        bool changed;
        lock (_stateLock)
        {
            changed = _gate.NotifySession(status);
        }

        if (!_gate.AllowsAutomaticAdb)
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _app.Log.Info($"PCVR session ({status.Kind}) does not use automatic headset ADB.");
            }

            if (_app.Adb.ActivityMode != AdbActivityMode.ManualCheck)
            {
                _app.Adb.NoteSessionIdle();
            }

            _app.RefreshTrayUi();
            return;
        }

        if (_app.Adb.ActivityMode != AdbActivityMode.ManualCheck)
        {
            _app.Adb.NoteSessionActive();
        }

        if (changed)
        {
            _app.Log.Info($"PCVR session active ({status.Kind}) — headset ADB is allowed.");
        }

        SyncWatch();
    }

    /// <summary>
    /// Confirmed PCVR session end. Stops our watcher and commands. Does not run adb kill-server
    /// and does not disconnect the Quest or any other device.
    /// </summary>
    public void NotifyPcvrSessionEnded()
    {
        if (!_app.Dispatcher.CheckAccess())
        {
            _app.Dispatcher.BeginInvoke(NotifyPcvrSessionEnded);
            return;
        }

        var wasActive = _gate.SessionActive || _timer.IsEnabled;
        lock (_stateLock)
        {
            _gate.NotifyEnded();
            ClearConnectionState();
        }

        var stoppedTimer = _timer.IsEnabled;
        _timer.Stop();
        if (wasActive || stoppedTimer)
        {
            _app.Adb.NoteSessionIdle();
            _app.Log.Info(
                "PCVR session ended — headset ADB polling stopped. The shared ADB server was left running.");
        }

        _app.RefreshTrayUi();
    }

    public void BeginManualAdb()
    {
        Interlocked.Increment(ref _manualDepth);
        lock (_stateLock)
        {
            _gate.BeginManual();
        }

        _app.Adb.NoteManualCheck();
    }

    public void EndManualAdb()
    {
        if (!_app.Dispatcher.CheckAccess())
        {
            _app.Dispatcher.BeginInvoke(EndManualAdb);
            return;
        }

        var depth = Interlocked.Decrement(ref _manualDepth);
        if (depth > 0)
        {
            return;
        }

        if (depth < 0)
        {
            Interlocked.Exchange(ref _manualDepth, 0);
        }

        lock (_stateLock)
        {
            _gate.EndManual();
        }

        _app.Adb.NoteManualCheckFinished(_gate.AllowsAutomaticAdb);
        SyncWatch();
    }

    /// <summary>
    /// True while automatic ADB is paused. Does not expire the pause — only <see cref="SyncWatch"/> does.
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
                _app.Dispatcher.BeginInvoke(SyncWatch);
                return false;
            }

            return true;
        }
    }

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
                return "Automatic headset ADB is paused. Manual actions still run when you request them.";
            }

            var local = until.Value.ToLocalTime();
            return $"Automatic headset ADB paused until {local:t} ({local:MMM d}). Manual actions still run when you request them.";
        }
    }

    public string ActivityStatusText =>
        !string.IsNullOrWhiteSpace(PauseStatusText)
            ? PauseStatusText
            : _app.Adb.DescribeCachedStatus();

    /// <summary>Suppress automatic session ADB. Manual actions still require an explicit request.</summary>
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
            ? $"Automatic headset ADB paused until {until.ToLocalTime():t}. Manual ADB actions still run when you request them."
            : "Automatic headset ADB paused until you resume. Manual ADB actions still run when you request them.";
        _app.Log.Info(message);
        if (notify)
        {
            _app.TrayNotify("ADB paused", message);
        }
    }

    /// <summary>Allow automatic ADB for a live PCVR session. Does not poll while no session is active.</summary>
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
        var sessionAllows = _gate.AllowsAutomaticAdb;
        _app.Log.Info(sessionAllows
            ? "Automatic headset ADB resumed for the active PCVR session."
            : "ADB resume allows automatic headset ADB when a PCVR session needs it. Not starting ADB while idle.");
        SyncWatch();
        if (notify)
        {
            _app.TrayNotify(
                "ADB resumed",
                sessionAllows
                    ? "Automatic headset ADB is on for this PCVR session."
                    : "Automatic headset ADB will wait for the next PCVR session.");
        }
    }

    /// <summary>Arm the watcher only while a PCVR session allows ADB and a feature needs it.</summary>
    public void SyncWatch()
    {
        if (!_app.Dispatcher.CheckAccess())
        {
            _app.Dispatcher.BeginInvoke(SyncWatch);
            return;
        }

        var timedPauseEnded = ExpireTimedPauseIfNeeded();
        ArmResumeTimer();

        var settings = _app.Settings.Current.Headset;
        if (!ShouldArmAutomaticWatcher(_gate.AllowsAutomaticAdb, settings))
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _app.Log.Info(settings.AdbWatcherPaused
                    ? "Automatic headset ADB is paused."
                    : _gate.AllowsAutomaticAdb
                        ? "Automatic headset ADB is off (apply-on-connect and wireless reconnect are both off)."
                        : "Headset ADB watcher idle — no confirmed PCVR session.");
            }

            _app.RefreshTrayUi();
            return;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _app.Log.Info(timedPauseEnded
                ? "Headset ADB watcher resumed after timed pause."
                : "Headset ADB watcher started for the active PCVR session.");
            BeginPoll();
        }

        ApplyCadence(_lastSerial is not null);
        _app.RefreshTrayUi();
    }

    public static bool ShouldArmAutomaticWatcher(bool automaticSessionAllowed, HeadsetSettings settings) =>
        AdbSessionGate.ShouldArmAutomaticWatcher(automaticSessionAllowed, settings);

    /// <summary>
    /// True when ADB lists a different transport serial than the last poll.
    /// USB hardware serials and wireless host:port strings are distinct, so a
    /// USB↔wireless flip is treated as a new headset connection.
    /// </summary>
    internal static bool IsNewAdbConnection(string? lastSerial, string serial) =>
        !string.Equals(lastSerial, serial, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Current policy: globals (Link/ODT) re-apply on every ADB apply, including reconnects,
    /// unless a personal game profile is already latched.
    /// </summary>
    internal static bool ShouldApplyGlobalBaselineOnAdbApply(bool gameProfileActive, bool applyGlobalWhenHeadsetConnects) =>
        !gameProfileActive && applyGlobalWhenHeadsetConnects;

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
        var willWatch = ShouldArmAutomaticWatcher(_gate.AllowsAutomaticAdb, settings);
        var message = willWatch
            ? "Timed ADB pause ended — headset watcher resumed."
            : "Timed ADB pause ended — automatic headset ADB stays idle until a PCVR session.";
        _app.Log.Info(message);
        _app.TrayNotify(
            "ADB resumed",
            willWatch
                ? "Timed pause ended. Headset ADB watching is on for this PCVR session."
                : "Timed pause ended. Automatic headset ADB stays idle until a PCVR session.");
        return willWatch;
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
        if (!ShouldArmAutomaticWatcher(_gate.AllowsAutomaticAdb, _app.Settings.Current.Headset))
        {
            return;
        }

        if (_app.LinkSessionWatch?.DeferAutomaticAdb == true)
        {
            return;
        }

        if (Interlocked.Exchange(ref _pollGate, 1) != 0)
        {
            return;
        }

        var generation = _gate.Generation;
        Task.Run(() =>
        {
            try
            {
                Poll(generation);
            }
            catch (Exception ex)
            {
                InvokeIfCurrent(generation, () => _app.Log.Warn($"Headset ADB: {ex.Message}"));
            }
            finally
            {
                Interlocked.Exchange(ref _pollGate, 0);
            }
        });
    }

    private void Poll(int generation)
    {
        if (!CanPoll(generation))
        {
            return;
        }

        MaybeAutoReconnectWireless(generation);

        if (!CanPoll(generation))
        {
            return;
        }

        var quest = _app.Adb.FindQuest();
        if (!CanPoll(generation))
        {
            return;
        }

        var serial = quest?.IsReady == true ? quest.Serial : null;
        if (serial is null)
        {
            SessionFlightRecorder.ObserveAdb(null, wireless: false, caller: nameof(HeadsetWatchService));
            string? was;
            lock (_stateLock)
            {
                if (!IsCurrent(generation))
                {
                    return;
                }

                was = _lastSerial;
                _lastSerial = null;
                _appliedForSerial = false;
            }

            if (was is not null)
            {
                InvokeIfCurrent(generation, () => _app.Log.Info($"ADB headset disconnected — was {was}."));
            }

            var ignored = _app.Adb.DescribeIgnoredDevices();
            if (!CanPoll(generation))
            {
                return;
            }

            if (ignored is not null && !string.Equals(ignored, _lastIgnoredMessage, StringComparison.Ordinal))
            {
                _lastIgnoredMessage = ignored;
                InvokeIfCurrent(generation, () => _app.Log.Info(ignored));
            }

            InvokeIfCurrent(generation, () => ApplyCadence(headsetPresent: false));
            return;
        }

        _lastIgnoredMessage = null;

        bool connected;
        lock (_stateLock)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            connected = IsNewAdbConnection(_lastSerial, serial);
            _lastSerial = serial;
        }

        var wireless = AdbService.LooksLikeWirelessSerial(serial);
        SessionFlightRecorder.ObserveAdb(serial, wireless, caller: nameof(HeadsetWatchService));
        if (connected)
        {
            var transport = wireless ? "wireless" : "USB";
            var label = string.IsNullOrWhiteSpace(quest?.Model) ? serial : $"{quest!.Model} ({serial})";
            InvokeIfCurrent(generation, () =>
                _app.Log.Info($"ADB headset connected ({transport}) — {label}."));
        }

        if (!connected && _appliedForSerial)
        {
            InvokeIfCurrent(generation, () => ApplyCadence(headsetPresent: true));
            return;
        }

        if (!_app.Settings.Current.Headset.ApplyWhenHeadsetConnects)
        {
            if (connected)
            {
                InvokeIfCurrent(generation, () =>
                {
                    const string message = "ADB headset connect — auto-apply is off (enable under Headset settings).";
                    _app.Log.Info(message);
                    _app.HeadsetAnnouncer.AnnounceHeadsetAction("ADB connected. Auto-apply is off.");
                });
            }

            InvokeIfCurrent(generation, () => ApplyCadence(headsetPresent: true));
            return;
        }

        if (!CanPoll(generation))
        {
            return;
        }

        try
        {
            var result = _app.Headset.Apply(_app.Settings.Current.Headset);
            if (!CanPoll(generation))
            {
                return;
            }

            SessionFlightRecorder.Mutation(
                "headset-adb",
                "Apply",
                result,
                connected ? "HeadsetWatchService.connect" : "HeadsetWatchService.reapply");
            string? global = null;
            if (ShouldApplyGlobalBaselineOnAdbApply(
                    _app.IsGameProfileActive,
                    _app.Settings.Current.ApplyGlobalWhenHeadsetConnects))
            {
                global = _app.ApplyGlobalBaseline(
                    notify: connected,
                    reason: connected ? "adb-connect" : "adb-reapply");
            }

            if (!CanPoll(generation))
            {
                return;
            }

            _appliedForSerial = true;
            InvokeIfCurrent(generation, () =>
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
                    _app.HeadsetAnnouncer.AnnounceHeadsetAction($"ADB connected. {result}");
                }

                ApplyCadence(headsetPresent: true);
            });
        }
        catch (Exception ex)
        {
            InvokeIfCurrent(generation, () =>
            {
                _app.Log.Warn($"Headset ADB: {ex.Message}");
                if (connected)
                {
                    _app.HeadsetAnnouncer.AnnounceHeadsetAction("ADB settings failed. Check Log.");
                }

                ApplyCadence(headsetPresent: true);
            });
        }
    }

    private void MaybeAutoReconnectWireless(int generation)
    {
        var settings = _app.Settings.Current.Headset;
        if (!CanPoll(generation)
            || !AdbSessionGate.ShouldAttemptWirelessReconnect(_gate.AllowsAutomaticAdb, settings))
        {
            return;
        }

        if (DateTime.UtcNow - _lastWirelessAttemptUtc < WirelessRetryInterval)
        {
            return;
        }

        if (_app.Adb.FindQuest()?.IsReady == true)
        {
            return;
        }

        if (!CanPoll(generation))
        {
            return;
        }

        _lastWirelessAttemptUtc = DateTime.UtcNow;
        var summary = _app.Adb.TryAutoReconnect(settings);
        if (!CanPoll(generation) || string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        if (summary.Contains("Connected", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Already connected", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("auto-reconnect", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("not a VR headset", StringComparison.OrdinalIgnoreCase))
        {
            InvokeIfCurrent(generation, () => _app.Log.Info(summary));
        }
    }

    private bool CanPoll(int generation)
    {
        if (_app.LinkSessionWatch?.DeferAutomaticAdb == true)
        {
            return false;
        }

        var settings = _app.Settings.Current.Headset;
        lock (_stateLock)
        {
            return IsCurrent(generation)
                   && ShouldArmAutomaticWatcher(_gate.AllowsAutomaticAdb, settings);
        }
    }

    private bool IsCurrent(int generation) => _gate.IsCurrent(generation);

    private void ClearConnectionState()
    {
        _lastSerial = null;
        _appliedForSerial = false;
        _lastIgnoredMessage = null;
        _lastWirelessAttemptUtc = DateTime.MinValue;
    }

    private void InvokeIfCurrent(int generation, Action action)
    {
        _app.Dispatcher.BeginInvoke(() =>
        {
            if (!IsCurrent(generation) || !_gate.AllowsAutomaticAdb)
            {
                return;
            }

            action();
        });
    }
}
