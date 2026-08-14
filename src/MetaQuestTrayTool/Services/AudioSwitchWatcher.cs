using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Audio policy (user boot settings stay until PCVR, then restore after):
/// 1. App / Windows start — never call SetDefault (leave desktop audio alone).
/// 2. PCVR session starts — switch to configured VR devices.
/// 3. PCVR session ends — restore configured fallback / desktop devices.
/// Idle polls are slow; active VR latch polls faster for a clean restore.
/// Timer stops entirely when auto-switch is disabled.
/// </summary>
public sealed class AudioSwitchWatcher : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool _armed;
    private bool _baselineHardware;
    private bool _baselineHeadsetAudio;
    private bool _vrDevicesApplied;
    private int _deadSessionHits;
    private DateTime _lastFallbackCaptureUtc = DateTime.MinValue;

    public AudioSwitchWatcher(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = IdleCadence.Quiet
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => SyncTimer();

    public void Dispose() => _timer.Stop();

    /// <summary>Start/stop from settings changes (tray / audio window).</summary>
    public void SyncTimer()
    {
        if (_app.Settings.Current.Audio.AutoSwitchEnabled)
        {
            if (!_timer.IsEnabled)
            {
                _armed = false;
                _timer.Start();
            }

            IdleCadence.Set(_timer, _vrDevicesApplied ? IdleCadence.Active : IdleCadence.Quiet);
        }
        else
        {
            _timer.Stop();
            _armed = false;
            _vrDevicesApplied = false;
            _deadSessionHits = 0;
        }
    }

    /// <summary>Immediate restore when Link / SteamVR session drop is detected elsewhere.</summary>
    public void NotifyPcvrSessionEnded(string reason)
    {
        if (!_app.Settings.Current.Audio.AutoSwitchEnabled || !_vrDevicesApplied)
        {
            return;
        }

        _deadSessionHits = 0;
        _baselineHardware = false;
        _baselineHeadsetAudio = false;
        RestoreFallback(reason);
        SyncTimer();
    }

    private void Poll()
    {
        var settings = _app.Settings.Current.Audio;
        if (!settings.AutoSwitchEnabled)
        {
            SyncTimer();
            return;
        }

        // While latched we switched output to the headset — do not treat "headset is still
        // default" as session-alive or audio never restores after PCVR exit.
        var hardware = IsPcvrSessionActive(settings, forExitWhileLatched: _vrDevicesApplied);
        var headsetAudio = !_vrDevicesApplied
                           && settings.Trigger == AudioSwitchTrigger.LinkAudioDevice
                           && _app.Audio.IsLinkAudioSessionActive(settings);
        var sessionAlive = hardware
                           || (settings.Trigger == AudioSwitchTrigger.LinkAudioDevice && headsetAudio);

        // Rarely remember desktop defaults — MMDevice enumeration is not free.
        if (!_vrDevicesApplied
            && !sessionAlive
            && DateTime.UtcNow - _lastFallbackCaptureUtc > TimeSpan.FromMinutes(2))
        {
            RememberDesktopFallback(settings);
            _lastFallbackCaptureUtc = DateTime.UtcNow;
        }

        // First poll after enable: snapshot baselines only — never SetDefault on launch.
        if (!_armed)
        {
            _armed = true;
            _baselineHardware = hardware;
            _baselineHeadsetAudio = headsetAudio;
            _vrDevicesApplied = false;
            _deadSessionHits = 0;
            _app.Log.Info(
                "Audio watcher: armed — leaving boot audio alone until a new PCVR session starts "
                + $"(hardware={(hardware ? "yes" : "no")}, headsetDefault={(headsetAudio ? "yes" : "no")}).");
            SyncTimer();
            return;
        }

        if (!_vrDevicesApplied)
        {
            // Rising edge only — leftover Meta virtual / sticky DeviceCache at boot is ignored.
            var hardwareStarted = hardware && !_baselineHardware;
            var headsetAudioStarted = headsetAudio && !_baselineHeadsetAudio;
            if (hardwareStarted || headsetAudioStarted)
            {
                SwitchToVr(hardwareStarted
                    ? "PCVR session started."
                    : "Headset became Windows default (Link audio) — treating as PCVR start.");
            }
            else
            {
                if (!hardware)
                {
                    _baselineHardware = false;
                }

                if (!headsetAudio)
                {
                    _baselineHeadsetAudio = false;
                }
            }

            SyncTimer();
            return;
        }

        // Latched: only live PCVR stream signals (SteamVR / Link streaming / VD) — not our own VR default.
        if (sessionAlive)
        {
            _deadSessionHits = 0;
            SyncTimer();
            return;
        }

        _deadSessionHits++;
        if (_deadSessionHits < 2)
        {
            SyncTimer();
            return;
        }

        _deadSessionHits = 0;
        _baselineHardware = false;
        _baselineHeadsetAudio = false;
        RestoreFallback("PCVR session ended — restoring desktop / fallback audio.");
        SyncTimer();
    }

    private bool IsPcvrSessionActive(AudioSwitchSettings settings, bool forExitWhileLatched)
    {
        if (forExitWhileLatched)
        {
            return IsLivePcvrStream();
        }

        if (settings.Trigger == AudioSwitchTrigger.OculusService)
        {
            _app.Oculus.Refresh();
            return _app.Oculus.IsServiceRunning;
        }

        try
        {
            // Match other watchers' probe args so the 5s cache is shared.
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            if (status.SessionActive)
            {
                return true;
            }
        }
        catch
        {
            // Probe optional.
        }

        return false;
    }

    /// <summary>
    /// True while a real PCVR stream is up — used to release the VR-audio latch on exit.
    /// Ignores DeviceCache auto-connect, ADB, and headset-as-default (we set that ourselves).
    /// </summary>
    private bool IsLivePcvrStream()
    {
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: true, includeAudioLink: true);
            if (status.SteamVrRunning || status.VirtualDesktopRunning)
            {
                return true;
            }

            return status.MetaLinkStreaming;
        }
        catch
        {
            return false;
        }
    }

    private void RememberDesktopFallback(AudioSwitchSettings audio)
    {
        var beforePlayback = audio.FallbackPlaybackDeviceId;
        var beforeRecording = audio.FallbackRecordingDeviceId;
        _app.Audio.CaptureCurrentAsFallback(audio);
        if (!string.Equals(beforePlayback, audio.FallbackPlaybackDeviceId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(beforeRecording, audio.FallbackRecordingDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            _app.Settings.Save();
        }
    }

    private void SwitchToVr(string reason)
    {
        var audio = _app.Settings.Current.Audio;
        if (audio.CaptureFallbackOnEachLinkSession
            || (string.IsNullOrWhiteSpace(audio.FallbackPlaybackDeviceId)
                && string.IsNullOrWhiteSpace(audio.FallbackRecordingDeviceId)))
        {
            var captured = _app.Audio.CaptureCurrentAsFallback(audio);
            _app.Settings.Save();
            _app.Log.Info(captured);
        }

        var result = _app.Audio.ApplyVrDevices(audio);
        _vrDevicesApplied = true;
        _deadSessionHits = 0;
        _baselineHardware = true;
        _baselineHeadsetAudio = true;
        _app.Log.Info($"{reason} Switched to VR audio. {result}");
    }

    private void RestoreFallback(string reason)
    {
        var result = _app.Audio.RestoreFallbackDevices(_app.Settings.Current.Audio);
        _vrDevicesApplied = false;
        _app.Log.Info($"{reason} {result}");
    }
}
