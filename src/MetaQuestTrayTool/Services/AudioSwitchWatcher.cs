using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches to VR audio while Link is active, then restores desktop hardware when Link drops.
/// Meta's "Oculus Virtual Audio Device" stays installed forever — presence alone is NOT Link.
/// Link is treated as active when Windows default output is the headset (Meta sets that on enter),
/// or when a removable headset endpoint appears. Startup heals leftover headset-default switches.
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
            Interval = TimeSpan.FromSeconds(3)
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
        // confirm with a slow HMD probe and restore speakers.
        if (_vrDevicesActive && active)
        {
            MaybeEndStaleVrSession(settings);
        }

        if (_wasActive is null)
        {
            _wasActive = active;
            // Never SwitchToVr on the first poll — old logic treated always-present
            // Oculus Virtual Audio as "already in Link" and stole the speakers every launch.
            if (active)
            {
                _vrDevicesActive = _app.Audio.IsCurrentPlaybackHeadset();
                _app.Log.Info("Audio watcher: headset already Windows default at startup — watching only (will not re-apply VR audio).");
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

        if (LooksLikeHeadsetIsReallyConnected())
        {
            _vrDevicesActive = true;
            _app.Log.Info("Audio watcher: headset is default and an HMD looks connected — leaving VR audio.");
            return;
        }

        RestoreFallback("Startup: headset was default but no connected HMD — restoring desktop speakers.");
    }

    private void MaybeEndStaleVrSession(AudioSwitchSettings settings)
    {
        // EnumHmd is relatively expensive — only every ~16 seconds while VR audio is latched.
        _hmdCheckCounter++;
        if (_hmdCheckCounter < 8)
        {
            return;
        }

        _hmdCheckCounter = 0;
        if (LooksLikeHeadsetIsReallyConnected())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.FallbackPlaybackDeviceId)
            && string.IsNullOrWhiteSpace(settings.FallbackRecordingDeviceId))
        {
            return;
        }

        _wasActive = false;
        RestoreFallback("Link appears ended (no HMD) but headset was still default — restoring desktop speakers.");
    }

    private bool LooksLikeHeadsetIsReallyConnected()
    {
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
