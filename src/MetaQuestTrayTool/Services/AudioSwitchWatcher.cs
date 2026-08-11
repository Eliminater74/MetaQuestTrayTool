using System.Windows.Threading;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Switches to configured VR audio devices while OVRService is running,
/// then restores fallback devices when the service stops.
/// </summary>
public sealed class AudioSwitchWatcher : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool? _wasRunning;
    private bool _vrDevicesActive;

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
            _wasRunning = null;
            return;
        }

        _app.Oculus.Refresh();
        var running = _app.Oculus.IsServiceRunning;

        if (_wasRunning is null)
        {
            _wasRunning = running;
            if (running && !_vrDevicesActive)
            {
                SwitchToVr("Oculus service is already running.");
            }

            return;
        }

        if (running == _wasRunning)
        {
            return;
        }

        _wasRunning = running;
        if (running)
        {
            SwitchToVr("Oculus service started.");
        }
        else
        {
            RestoreFallback("Oculus service stopped.");
        }
    }

    private void SwitchToVr(string reason)
    {
        var audio = _app.Settings.Current.Audio;
        if (string.IsNullOrWhiteSpace(audio.FallbackPlaybackDeviceId)
            && string.IsNullOrWhiteSpace(audio.FallbackRecordingDeviceId))
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
        _app.Log.Info($"{reason} Restored fallback audio. {result}");
    }
}
