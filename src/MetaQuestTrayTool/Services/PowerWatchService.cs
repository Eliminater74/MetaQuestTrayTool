using System.Windows.Threading;
using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

public sealed class PowerWatchService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool? _wasRunning;
    private bool _vrPlanActive;

    public PowerWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = IdleCadence.Quiet
        };
        _timer.Tick += (_, _) => Poll();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Start()
    {
        SyncTimer();
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    /// <summary>Start/stop the poll timer from settings changes (wake resume still uses SystemEvents).</summary>
    public void SyncTimer()
    {
        var settings = _app.Settings.Current.Power;
        var needsPoll = settings.AutoSwitchEnabled
                        && settings.ApplyOn != Models.PowerPlanTrigger.ToolStartExit;
        if (needsPoll)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            IdleCadence.Set(_timer, _vrPlanActive ? IdleCadence.Watching : IdleCadence.Quiet);
        }
        else
        {
            _timer.Stop();
            _wasRunning = null;
        }
    }

    private void Poll()
    {
        var settings = _app.Settings.Current.Power;
        if (!settings.AutoSwitchEnabled || settings.ApplyOn == Models.PowerPlanTrigger.ToolStartExit)
        {
            _wasRunning = null;
            SyncTimer();
            return;
        }

        // LinkAudioSession and OculusService both currently key off OVRService running state.
        _app.Oculus.Refresh();
        var running = _app.Oculus.IsServiceRunning;
        if (_wasRunning is null)
        {
            _wasRunning = running;
            if (running && !_vrPlanActive)
            {
                ApplyVr("Oculus service is already running.");
            }

            SyncTimer();
            return;
        }

        if (running == _wasRunning)
        {
            SyncTimer();
            return;
        }

        _wasRunning = running;
        if (running)
        {
            ApplyVr("Oculus service started.");
        }
        else
        {
            Restore("Oculus service stopped.");
        }

        SyncTimer();
    }

    private void ApplyVr(string reason)
    {
        var settings = _app.Settings.Current.Power;
        var planResult = _app.Power.ApplyVrPlan(settings);
        _app.Settings.Save();
        _vrPlanActive = true;
        _app.Log.Info($"{reason} {planResult}");

        if (settings.DisableUsbSelectiveSuspendWhileRunning)
        {
            _app.Log.Info(_app.Power.SetUsbSelectiveSuspend(enabled: false));
        }
    }

    private void Restore(string reason)
    {
        var result = _app.Power.RestoreFallbackPlan(_app.Settings.Current.Power);
        _vrPlanActive = false;
        _app.Log.Info($"{reason} {result}");
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        var restart = _app.Settings.Current.Service.RestartServiceWhenComputerWakes
                      || _app.Settings.Current.Power.RestartServiceAfterSleep;
        if (!restart)
        {
            return;
        }

        _app.Log.Info("System resumed from sleep — restarting OVRService.");
        var result = _app.Oculus.Restart();
        _app.Log.Info(result);
    }
}
