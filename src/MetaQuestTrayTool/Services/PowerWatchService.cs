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

        var running = IsVrPowerSessionActive(settings);
        if (_wasRunning is null)
        {
            _wasRunning = running;
            if (running && !_vrPlanActive)
            {
                ApplyVr("VR power session already active.");
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
            ApplyVr(settings.ApplyOn == Models.PowerPlanTrigger.OculusService
                ? "Oculus service started."
                : "PCVR / Link session started.");
        }
        else
        {
            Restore(settings.ApplyOn == Models.PowerPlanTrigger.OculusService
                ? "Oculus service stopped."
                : "PCVR / Link session ended.");
        }

        SyncTimer();
    }

    private bool IsVrPowerSessionActive(Models.PowerSettings settings)
    {
        if (settings.ApplyOn == Models.PowerPlanTrigger.OculusService)
        {
            _app.Oculus.Refresh();
            return _app.Oculus.IsServiceRunning;
        }

        // LinkAudioSession — live PCVR stream or headset as Windows default (Link audio).
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            if (status.SteamVrRunning || status.VirtualDesktopRunning || status.MetaLinkStreaming)
            {
                return true;
            }

            return _app.Audio.IsLinkAudioSessionActive(_app.Settings.Current.Audio);
        }
        catch
        {
            return false;
        }
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

        _app.Dispatcher.BeginInvoke(() =>
        {
            _app.LinkSessionWatch?.NotifySystemResumed();

            var restart = _app.Settings.Current.Service.RestartServiceWhenComputerWakes
                          || _app.Settings.Current.Power.RestartServiceAfterSleep;
            if (!restart)
            {
                return;
            }

            _app.Log.Info("System resumed from sleep — restarting OVRService.");
            Task.Run(() =>
            {
                try
                {
                    var result = _app.Oculus.Restart();
                    _app.Dispatcher.BeginInvoke(() => _app.Log.Info(result));
                }
                catch (Exception ex)
                {
                    _app.Dispatcher.BeginInvoke(() =>
                        _app.Log.Warn("OVRService restart after sleep failed: " + ex.Message));
                }
            });
        });
    }
}
