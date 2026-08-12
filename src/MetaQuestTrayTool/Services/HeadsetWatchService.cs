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

    public HeadsetWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        _timer.Start();
        _app.Log.Info("Headset ADB watcher started.");
        Poll();
    }

    public void Dispose() => _timer.Stop();

    private void Poll()
    {
        try
        {
            var quest = _app.Adb.FindQuest();
            var serial = quest?.IsReady == true ? quest.Serial : null;
            if (serial is null)
            {
                _lastSerial = null;
                _appliedForSerial = false;
                var ignored = _app.Adb.DescribeIgnoredDevices();
                if (ignored is not null && !string.Equals(ignored, _lastIgnoredMessage, StringComparison.Ordinal))
                {
                    _lastIgnoredMessage = ignored;
                    _app.Log.Info(ignored);
                }

                return;
            }

            _lastIgnoredMessage = null;

            var connected = !string.Equals(_lastSerial, serial, StringComparison.OrdinalIgnoreCase);
            _lastSerial = serial;
            if (!connected && _appliedForSerial)
            {
                return;
            }

            if (!_app.Settings.Current.Headset.ApplyWhenHeadsetConnects)
            {
                return;
            }

            var result = _app.Headset.Apply(_app.Settings.Current.Headset);
            _app.Settings.Save();
            _app.Log.Info(result);
            _appliedForSerial = true;

            if (!_app.IsGameProfileActive
                && _app.Settings.Current.ApplyGlobalWhenHeadsetConnects)
            {
                var global = _app.ApplyGlobalBaseline(notify: connected);
                _app.Log.Info(global);
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
    }
}
