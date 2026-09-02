using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Short-lived view of the expensive runtime probes used by tray, Status, Info, and readiness UI.
/// </summary>
public sealed class RuntimeSnapshot
{
    public required DateTimeOffset CapturedAt { get; init; }
    public required VrConnectionStatus Link { get; init; }
    public required SteamVrInstallInfo SteamVr { get; init; }
    public required OpenXrRuntimeKind? OpenXr { get; init; }
    public required string? OpenXrPath { get; init; }
    public required OpenXrAlignment OpenXrAlignment { get; init; }
    public required PcvrSetupMode SetupMode { get; init; }
    public required bool SteamVrOpenXrAvailable { get; init; }
    public required GpuRecommendation? Gpu { get; init; }
    public HeadsetIdentity? Headset { get; init; }
    public bool IncludesHeadset => Headset is not null;

    public required bool OculusServiceExists { get; init; }
    public required bool OculusServiceRunning { get; init; }
    public required string OculusServiceStatus { get; init; }
    public required string? OculusInstallPath { get; init; }

    public required bool IsElevated { get; init; }
    public required bool IsGameProfileActive { get; init; }
    public required string? ActiveProfileName { get; init; }
    public required bool HotKeysEnabled { get; init; }
    public required bool VoiceEnabled { get; init; }
    public required bool AudioAutoSwitchEnabled { get; init; }
    public required bool PreventDashLaunchEffective { get; init; }
    public PowerPlanInfo? ActivePowerPlan { get; init; }
}

/// <summary>
/// Centralizes short-TTL probe caching so UI and watchers can reuse one coherent runtime view.
/// </summary>
public sealed class RuntimeSnapshotService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(3);

    private readonly App _app;
    private readonly object _sync = new();
    private RuntimeSnapshot? _cached;

    public RuntimeSnapshotService(App app) => _app = app;

    public void Invalidate()
    {
        lock (_sync)
        {
            _cached = null;
        }
    }

    public RuntimeSnapshot? TryGetCached(bool requireHeadset = false)
    {
        lock (_sync)
        {
            if (_cached is null
                || DateTimeOffset.UtcNow - _cached.CapturedAt >= CacheTtl
                || (requireHeadset && !_cached.IncludesHeadset))
            {
                return null;
            }

            return _cached;
        }
    }

    public RuntimeSnapshot Capture(bool includeHeadset = false, bool force = false)
    {
        if (!force)
        {
            lock (_sync)
            {
                if (_cached is not null
                    && DateTimeOffset.UtcNow - _cached.CapturedAt < CacheTtl
                    && (!includeHeadset || _cached.IncludesHeadset))
                {
                    return _cached;
                }
            }
        }

        var snapshot = Build(includeHeadset);
        lock (_sync)
        {
            _cached = snapshot;
        }

        return snapshot;
    }

    private RuntimeSnapshot Build(bool includeHeadset)
    {
        _app.Oculus.Refresh();
        var link = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
        var steamVr = _app.SteamVrInstall.Probe();
        var openXr = _app.OpenXr.ReadActiveKind();
        var openXrPath = _app.OpenXr.ReadActivePath();
        var openXrAlignment = PcvrSetup.EvaluateOpenXr(_app, openXr);
        var setupMode = PcvrSetup.GetMode(_app);
        var steamVrOpenXrAvailable = _app.OpenXr.IsAvailable(OpenXrRuntimeKind.SteamVr);
        var gpu = _app.Gpu.GetRecommendation();
        var activePowerPlan = TryGetActivePowerPlan();
        HeadsetIdentity? headset = null;
        if (includeHeadset)
        {
            headset = _app.Headset.ReadIdentity(_app.Settings.Current.Headset);
        }

        var dash = _app.Settings.Current.DashToSteamVr;
        var preventDash = _app.DashToSteamVr.IsPreventDashLaunchEnabled() || dash.PreferPreventDashLaunch;

        return new RuntimeSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Link = link,
            SteamVr = steamVr,
            OpenXr = openXr,
            OpenXrPath = openXrPath,
            OpenXrAlignment = openXrAlignment,
            SetupMode = setupMode,
            SteamVrOpenXrAvailable = steamVrOpenXrAvailable,
            Gpu = gpu,
            Headset = headset,
            OculusServiceExists = _app.Oculus.ServiceExists,
            OculusServiceRunning = _app.Oculus.IsServiceRunning,
            OculusServiceStatus = _app.Oculus.ServiceStatus,
            OculusInstallPath = _app.Oculus.InstallPath,
            IsElevated = _app.StartupRegistration.IsProcessElevated,
            IsGameProfileActive = _app.IsGameProfileActive,
            ActiveProfileName = _app.ActiveProfileName,
            HotKeysEnabled = _app.Settings.Current.HotKeys.Enabled,
            VoiceEnabled = _app.Settings.Current.Voice.Enabled,
            AudioAutoSwitchEnabled = _app.Settings.Current.Audio.AutoSwitchEnabled,
            PreventDashLaunchEffective = preventDash,
            ActivePowerPlan = activePowerPlan
        };
    }

    private PowerPlanInfo? TryGetActivePowerPlan()
    {
        try
        {
            return _app.Power.GetActivePlan();
        }
        catch
        {
            return null;
        }
    }
}
