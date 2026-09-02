using System.Collections.ObjectModel;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>Builds the live Status page chip grid from current probes.</summary>
public sealed class StatusDashboardService
{
    private readonly App _app;

    public StatusDashboardService(App app) => _app = app;

    public IReadOnlyList<StatusChipVm> BuildChips()
    {
        _app.Oculus.Refresh();
        var link = _app.LinkConnection.Probe(includeEnumHmd: false);
        var steamVr = _app.SteamVrInstall.Probe();
        var openXr = _app.OpenXr.ReadActiveKind();
        var openXrAlignment = PcvrSetup.EvaluateOpenXr(_app, openXr);
        var setupMode = PcvrSetup.GetMode(_app);
        var openXrPath = _app.OpenXr.ReadActivePath();
        var headset = _app.Headset.ReadIdentity(_app.Settings.Current.Headset);
        var runtime = headset.IsReady
            ? _app.Adb.ReadRuntimeStatus(headset.AdbSerial ?? headset.Serial ?? string.Empty)
            : null;
        var ready = _app.PcvrReady.Evaluate();
        var gpu = _app.Gpu.GetRecommendation();
        var elevated = _app.StartupRegistration.IsProcessElevated;
        var profileActive = _app.IsGameProfileActive;
        var hotKeys = _app.Settings.Current.HotKeys.Enabled;
        var voice = _app.Settings.Current.Voice.Enabled;
        var dash = _app.Settings.Current.DashToSteamVr;
        var preventDash = _app.DashToSteamVr.IsPreventDashLaunchEnabled() || dash.PreferPreventDashLaunch;

        var chips = new List<StatusChipVm>
        {
            Chip("PCVR Ready", ready.ShortTraySummary, ready.Summary,
                ready.Overall switch
                {
                    PcvrReadyLevel.Ok => StatusChipKind.On,
                    PcvrReadyLevel.Warn => StatusChipKind.Warn,
                    _ => StatusChipKind.Fail
                }),

            steamVr.IsInstalled
                ? Chip("SteamVR", steamVr.IsRunning ? "Running" : "Installed",
                    $"{steamVr.ChannelLabel}" +
                    (string.IsNullOrWhiteSpace(steamVr.Version) ? "" : $" · v{steamVr.Version}") +
                    (string.IsNullOrWhiteSpace(steamVr.BuildId) ? "" : $" · build {steamVr.BuildId}"),
                    steamVr.IsRunning
                        ? StatusChipKind.On
                        : steamVr.Channel == SteamVrChannel.Beta
                            ? StatusChipKind.Warn
                            : StatusChipKind.Warn,
                    steamVr.IsRunning ? null : "steamvr-start",
                    steamVr.IsRunning ? null : "Start")
                : Chip("SteamVR", "Not installed", "Required for most Steam PCVR games",
                    StatusChipKind.Fail, "steamvr-install", "Install"),

            Chip("SteamVR channel",
                steamVr.IsInstalled ? steamVr.ChannelLabel : "—",
                steamVr.IsInstalled
                    ? (steamVr.Channel == SteamVrChannel.Beta
                        ? "Beta builds can be less stable for PCVR"
                        : "Stable branch")
                    : "Install SteamVR first",
                !steamVr.IsInstalled
                    ? StatusChipKind.Off
                    : steamVr.Channel == SteamVrChannel.Beta
                        ? StatusChipKind.Warn
                        : StatusChipKind.On),

            Chip("OpenXR",
                openXr.HasValue ? OpenXrRuntimeService.Label(openXr) : "Not set",
                openXrAlignment.Detail
                + (string.IsNullOrWhiteSpace(openXrPath) ? "" : Environment.NewLine + openXrPath),
                PcvrSetup.ChipKindFor(openXrAlignment.Level),
                openXrAlignment.CanFix && _app.OpenXr.IsAvailable(PcvrSetup.ExpectedOpenXr(_app))
                    ? "openxr-expected"
                    : null,
                openXrAlignment.FixLabel),

            Chip("PCVR setup",
                PcvrSetup.ModeLabel(setupMode),
                setupMode == PcvrSetupMode.SteamVrOverMetaLink
                    ? "PreventDashLaunch blocks Dash; SteamVR + SteamVR OpenXR on Meta Link connect"
                    : "Meta Dash / Meta OpenXR — enable PreventDash on Service & Startup for Steam-only Link",
                setupMode == PcvrSetupMode.SteamVrOverMetaLink ? StatusChipKind.On : StatusChipKind.Off),

            Chip("OVRService",
                _app.Oculus.ServiceStatus,
                _app.Oculus.InstallPath ?? "Meta Quest PC app path unknown",
                _app.Oculus.IsServiceRunning ? StatusChipKind.On : StatusChipKind.Fail,
                !_app.Oculus.IsServiceRunning ? "ovrservice" : null,
                !_app.Oculus.IsServiceRunning ? "Start" : null),

            Chip("Administrator",
                elevated ? "Elevated" : "Normal user",
                elevated
                    ? "Mid-VR actions skip UAC — use HotKeys / voice (SteamVR cannot click elevated tray)"
                    : "OpenXR / service changes may need UAC — enable Admin at logon",
                elevated ? StatusChipKind.On : StatusChipKind.Warn),

            Chip("PCVR session",
                link.SessionActive ? "Active" : "Idle",
                link.InfoBanner,
                link.SessionActive ? StatusChipKind.On : StatusChipKind.Off),

            Chip("Link / stream",
                DescribeLinkKind(link),
                string.IsNullOrWhiteSpace(link.Detail) ? link.InfoBanner : link.Detail,
                link.MetaLinkStreaming || link.SteamVrRunning || link.VirtualDesktopRunning
                    ? StatusChipKind.On
                    : StatusChipKind.Off),

            Chip("SteamVR process",
                link.SteamVrRunning || steamVr.IsRunning ? "Yes" : "No",
                "vrserver / SteamVR runtime process",
                link.SteamVrRunning || steamVr.IsRunning ? StatusChipKind.On : StatusChipKind.Off),

            Chip("Virtual Desktop",
                link.VirtualDesktopRunning ? "Running" : "Off",
                link.VirtualDesktopRunning
                    ? "VD session — Meta Link/ODT gated"
                    : "Not detected",
                link.VirtualDesktopRunning ? StatusChipKind.On : StatusChipKind.Off),

            Chip("Headset ADB",
                headset.IsReady ? "Connected" : "Not connected",
                headset.IsReady
                    ? $"{headset.Model ?? "Quest"} · {headset.Serial ?? headset.AdbSerial}"
                    : "Optional — USB or wireless debugging for headset props",
                headset.IsReady
                    ? StatusChipKind.On
                    : headset.IsRogue
                        ? StatusChipKind.Fail
                        : StatusChipKind.Off),

            Chip("Battery / Wi‑Fi",
                runtime?.Available == true ? runtime.Summary : "—",
                runtime?.Available == true
                    ? "From ADB dumpsys"
                    : "Connect ADB to read battery and Wi‑Fi",
                runtime?.Available == true ? StatusChipKind.On : StatusChipKind.Off),

            Chip("Active profile",
                profileActive ? (_app.ActiveProfileName ?? "Yes") : "None",
                profileActive
                    ? "Personal profile applied — globals restore when the game exits"
                    : "No watched game profile active",
                profileActive ? StatusChipKind.On : StatusChipKind.Off),

            Chip("HotKeys",
                hotKeys ? "On" : "Off",
                hotKeys
                    ? "Primary mid-session control in SteamVR"
                    : "Enable on Tray Tool — elevated tray is not clickable in SteamVR",
                hotKeys ? StatusChipKind.On : StatusChipKind.Warn),

            Chip("Voice",
                voice ? "On" : "Off",
                voice
                    ? "Hands-free commands via push-to-talk / always-on"
                    : "Optional — enable on Tray Tool for in-VR voice control",
                voice ? StatusChipKind.On : StatusChipKind.Off),

            Chip("Dash → SteamVR",
                preventDash ? "Armed" : "Off",
                preventDash
                    ? "PreventDashLaunch — auto SteamVR on Meta Link; no Meta process killing"
                    : "Enable PreventDashLaunch on Service & Startup",
                preventDash ? StatusChipKind.On : StatusChipKind.Off),

            Chip("GPU",
                gpu?.Adapter.Name ?? "Unknown",
                gpu is null
                    ? "Could not detect a display adapter"
                    : $"{gpu.Adapter.TierLabel} · {gpu.Adapter.DedicatedMemoryLabel}",
                gpu is null ? StatusChipKind.Warn : StatusChipKind.On),

            Chip("Audio switch",
                _app.Settings.Current.Audio.AutoSwitchEnabled ? "On" : "Off",
                _app.Settings.Current.Audio.AutoSwitchEnabled
                    ? "Switches to VR devices when Link audio is active"
                    : "Disabled — configure on Tray Tool",
                _app.Settings.Current.Audio.AutoSwitchEnabled ? StatusChipKind.On : StatusChipKind.Off)
        };

        return chips;
    }

    public string RunAction(string actionId) => actionId switch
    {
        "steamvr-install" => _app.SteamVrInstall.OpenInstallPage(),
        "steamvr-start" => _app.DashToSteamVr.StartSteamVrNow("Status chip"),
        "openxr-steamvr" => SetOpenXr(OpenXrRuntimeKind.SteamVr),
        "openxr-expected" => FixOpenXrForSetup(),
        "ovrservice" => _app.Oculus.Start(),
        _ => $"Unknown status action: {actionId}"
    };

    private string FixOpenXrForSetup()
    {
        var target = PcvrSetup.ExpectedOpenXr(_app);
        var settings = _app.Settings.Current.OpenXr;
        settings.PreferredRuntime = target;
        settings.ApplyOnStart = true;
        _app.Settings.Save();
        return SetOpenXr(target);
    }

    private string SetOpenXr(OpenXrRuntimeKind target)
    {
        var result = _app.OpenXr.Set(target);
        _app.HeadsetAnnouncer.AnnounceActionResult(
            $"OpenXR switched to {OpenXrRuntimeService.Label(target)}",
            result);
        return result;
    }

    private static StatusChipVm Chip(
        string title,
        string value,
        string? detail,
        StatusChipKind kind,
        string? actionId = null,
        string? actionLabel = null) => new()
        {
            Title = title,
            Value = value,
            Detail = detail,
            Kind = kind,
            ActionId = actionId,
            ActionLabel = actionLabel
        };

    private static string DescribeLinkKind(VrConnectionStatus link) => link.Kind switch
    {
        VrConnectionKind.MetaAirLink => "Air Link",
        VrConnectionKind.MetaWiredLink => "Wired Link",
        VrConnectionKind.SteamLinkOrSteamVr => "Steam Link / SteamVR",
        VrConnectionKind.VirtualDesktop => "Virtual Desktop",
        VrConnectionKind.Idle => "Idle",
        _ => link.Kind.ToString()
    };
}
