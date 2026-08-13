using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Audio policy (user boot settings stay until PCVR, then restore after):
/// 1. App / Windows start — never call SetDefault (leave desktop audio alone).
/// 2. PCVR session starts — switch to configured VR devices.
/// 3. PCVR session ends — restore configured fallback / desktop devices.
/// Idle polls are slow; active VR latch polls faster for a clean restore.
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

    public void Start()
    {
        _timer.Start();
        ApplyCadence();
    }

    public void Dispose() => _timer.Stop();

    private void ApplyCadence()
    {
        if (!_app.Settings.Current.Audio.AutoSwitchEnabled)
        {
            IdleCadence.Set(_timer, IdleCadence.HeavyIdle);
            return;
        }

        IdleCadence.Set(_timer, _vrDevicesApplied ? IdleCadence.Active : IdleCadence.Quiet);
    }

    private void Poll()
    {
        var settings = _app.Settings.Current.Audio;
        if (!settings.AutoSwitchEnabled)
        {
            _armed = false;
            _vrDevicesApplied = false;
            _deadSessionHits = 0;
            ApplyCadence();
            return;
        }

        var hardware = IsHardwarePcvrSession(settings);
        var headsetAudio = settings.Trigger == AudioSwitchTrigger.LinkAudioDevice
                           && _app.Audio.IsLinkAudioSessionActive(settings);

        // Rarely remember desktop defaults — MMDevice enumeration is not free.
        if (!_vrDevicesApplied
            && !hardware
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
            ApplyCadence();
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

            ApplyCadence();
            return;
        }

        // VR devices applied — restore only when the hardware session is gone.
        if (hardware)
        {
            _deadSessionHits = 0;
            ApplyCadence();
            return;
        }

        _deadSessionHits++;
        if (_deadSessionHits < 2)
        {
            ApplyCadence();
            return;
        }

        _deadSessionHits = 0;
        _baselineHardware = false;
        _baselineHeadsetAudio = headsetAudio;
        RestoreFallback("PCVR session ended — restoring desktop / fallback audio.");
        ApplyCadence();
    }

    private bool IsHardwarePcvrSession(AudioSwitchSettings settings)
    {
        if (settings.Trigger == AudioSwitchTrigger.OculusService)
        {
            _app.Oculus.Refresh();
            return _app.Oculus.IsServiceRunning;
        }

        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: false);
            if (status.SessionActive)
            {
                return true;
            }
        }
        catch
        {
            // Probe optional.
        }

        try
        {
            var quest = _app.Headset.ReadIdentity(_app.Settings.Current.Headset);
            if (quest.IsVrHeadset && quest.IsReady && !quest.IsRogue)
            {
                return true;
            }
        }
        catch
        {
            // ADB optional.
        }

        return false;
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
