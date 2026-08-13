using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches to VR audio while Link is active, then restores desktop hardware when Link drops.
/// Meta's "Oculus Virtual Audio Device" stays installed forever — presence alone is NOT Link.
/// Link is treated as active when Windows default output is the headset (Meta sets that on enter),
/// or when a removable headset endpoint appears.
/// Startup heal only restores Speakers when no real PCVR session is detected (SteamVR / VD /
/// DeviceCache / ADB) — never because ADB/EnumHmd alone failed during Air Link.
/// </summary>
public sealed class AudioSwitchWatcher : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool? _wasActive;
    private bool _vrDevicesActive;
    private bool _startupHealDone;
    private int _hmdCheckCounter;

    public AudioSwitchWatcher(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();

    public void Dispose() => _timer.Stop();

    private void Poll()
    {
        var settings = _app.Settings.Current.Audio;
        if (!settings.AutoSwitchEnabled)
        {
            _wasActive = null;
            _vrDevicesActive = false;
            return;
        }

        if (!_startupHealDone)
        {
            TryStartupHeal(settings);
        }

        // While on desktop, keep remembering non-headset defaults for a clean restore later.
        var active = IsSessionActive(settings);
        if (!active && !_vrDevicesActive)
        {
            RememberDesktopFallback(settings);
        }

        // If we switched to VR earlier but Meta left the headset as default after Link ended,
        // confirm with a real PCVR session probe (not ADB-only) and restore speakers.
        if (_vrDevicesActive && active)
        {
            MaybeEndStaleVrSession(settings);
        }

        if (_wasActive is null)
        {
            _wasActive = active;
            // Never treat always-present Oculus Virtual Audio as "already in Link" without
            // applying the user's configured VR devices — that left wrong endpoints in place.
            if (active)
            {
                _vrDevicesActive = true;
                if (HasConfiguredVrDevices(settings))
                {
                    SwitchToVr("Audio watcher: headset already Windows default at startup — applying configured VR devices.");
                }
                else
                {
                    _app.Log.Info("Audio watcher: headset already Windows default at startup — watching (no VR devices configured).");
                }
            }
            else
            {
                _app.Log.Info("Audio watcher: desktop audio at startup.");
            }

            return;
        }

        if (active == _wasActive)
        {
            return;
        }

        _wasActive = active;
        if (active)
        {
            SwitchToVr("Link / headset audio became active.");
        }
        else
        {
            RestoreFallback("Link / headset audio became inactive.");
        }
    }

    private bool IsSessionActive(AudioSwitchSettings settings) =>
        settings.Trigger switch
        {
            AudioSwitchTrigger.OculusService => IsOculusServiceRunning(),
            _ => _app.Audio.IsLinkAudioSessionActive(settings)
        };

    private bool IsOculusServiceRunning()
    {
        _app.Oculus.Refresh();
        return _app.Oculus.IsServiceRunning;
    }

    private static bool HasConfiguredVrDevices(AudioSwitchSettings audio) =>
        !string.IsNullOrWhiteSpace(audio.VrPlaybackDeviceId)
        || !string.IsNullOrWhiteSpace(audio.VrRecordingDeviceId)
        || !string.IsNullOrWhiteSpace(audio.VrCommunicationsPlaybackDeviceId)
        || !string.IsNullOrWhiteSpace(audio.VrCommunicationsRecordingDeviceId);

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

    private void TryStartupHeal(AudioSwitchSettings settings)
    {
        _startupHealDone = true;
        if (!_app.Audio.IsCurrentPlaybackHeadset())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.FallbackPlaybackDeviceId)
            && string.IsNullOrWhiteSpace(settings.FallbackRecordingDeviceId))
        {
            return;
        }

        if (PcvrSessionLooksAlive())
        {
            _vrDevicesActive = true;
            _app.Log.Info("Audio watcher: headset is default and a PCVR session looks live — leaving / applying VR audio.");
            return;
        }

        RestoreFallback("Startup: headset was default but no live PCVR session — restoring desktop speakers.");
    }

    private void MaybeEndStaleVrSession(AudioSwitchSettings settings)
    {
        // PCVR probe is relatively expensive — only every ~20 seconds while VR audio is latched.
        _hmdCheckCounter++;
        if (_hmdCheckCounter < 4)
        {
            return;
        }

        _hmdCheckCounter = 0;
        if (PcvrSessionLooksAlive())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.FallbackPlaybackDeviceId)
            && string.IsNullOrWhiteSpace(settings.FallbackRecordingDeviceId))
        {
            return;
        }

        _wasActive = false;
        RestoreFallback("Link appears ended (no live PCVR session) but headset was still default — restoring desktop speakers.");
    }

    /// <summary>
    /// True when SteamVR / Virtual Desktop / Meta DeviceCache / ADB / EnumHmd indicate a live
    /// session. Intentionally ignores "headset is Windows default" alone — that stays true after
    /// Link ends and would otherwise prevent restoring Speakers.
    /// </summary>
    private bool PcvrSessionLooksAlive()
    {
        try
        {
            // includeAudioLink: false — leftover Meta virtual default must not count as a session.
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
            // ADB optional for Air Link.
        }

        if (!_app.DebugTool.IsAvailable)
        {
            return false;
        }

        try
        {
            return _app.DebugTool.EnumerateHeadsets().Count > 0;
        }
        catch
        {
            return false;
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
        _vrDevicesActive = true;
        _hmdCheckCounter = 0;
        _app.Log.Info($"{reason} Switched to VR audio. {result}");
    }

    private void RestoreFallback(string reason)
    {
        var result = _app.Audio.RestoreFallbackDevices(_app.Settings.Current.Audio);
        _vrDevicesActive = false;
        _app.Log.Info($"{reason} Restored desktop/fallback audio. {result}");
    }
}
