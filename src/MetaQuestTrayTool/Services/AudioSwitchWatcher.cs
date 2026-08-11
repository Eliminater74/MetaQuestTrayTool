using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches to VR audio while Link is active, then restores desktop hardware when Link drops.
/// Default trigger watches headset audio endpoints — not OVRService — because the service often
/// stays running after Air Link / USB Link disconnects.
/// </summary>
public sealed class AudioSwitchWatcher : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool? _wasActive;
    private bool _vrDevicesActive;

    public AudioSwitchWatcher(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
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
            return;
        }

        var active = IsSessionActive(settings);
        if (_wasActive is null)
        {
            _wasActive = active;
            if (active && !_vrDevicesActive)
            {
                SwitchToVr("Link audio session already active.");
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
        _app.Log.Info($"{reason} Switched to VR audio. {result}");
    }

    private void RestoreFallback(string reason)
    {
        var result = _app.Audio.RestoreFallbackDevices(_app.Settings.Current.Audio);
        _vrDevicesActive = false;
        _app.Log.Info($"{reason} Restored desktop/fallback audio. {result}");
    }
}
