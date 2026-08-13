using System.Diagnostics;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public enum PcvrReadyLevel
{
    Ok,
    Warn,
    Fail
}

public sealed class PcvrReadyItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required PcvrReadyLevel Level { get; init; }
    public string? FixLabel { get; init; }
    public bool CanFix { get; init; }
}

public sealed class PcvrReadyReport
{
    public required IReadOnlyList<PcvrReadyItem> Items { get; init; }
    public int OkCount => Items.Count(i => i.Level == PcvrReadyLevel.Ok);
    public int WarnCount => Items.Count(i => i.Level == PcvrReadyLevel.Warn);
    public int FailCount => Items.Count(i => i.Level == PcvrReadyLevel.Fail);
    public int Total => Items.Count;

    public PcvrReadyLevel Overall =>
        FailCount > 0 ? PcvrReadyLevel.Fail
        : WarnCount > 0 ? PcvrReadyLevel.Warn
        : PcvrReadyLevel.Ok;

    public string Summary => Overall switch
    {
        PcvrReadyLevel.Ok => $"PCVR ready — {OkCount}/{Total} checks passed",
        PcvrReadyLevel.Warn => $"PCVR mostly ready — {OkCount}/{Total} ok, {WarnCount} warning(s)",
        _ => $"PCVR not ready — {FailCount} issue(s), {WarnCount} warning(s)"
    };

    public string ShortTraySummary
    {
        get
        {
            var mark = Overall switch
            {
                PcvrReadyLevel.Ok => "OK",
                PcvrReadyLevel.Warn => "Warn",
                _ => "Fix"
            };
            return $"{mark} {OkCount}/{Total}";
        }
    }
}

/// <summary>
/// Pre-flight checklist before putting the headset on — SteamVR-biased for Steam-first PCVR.
/// </summary>
public sealed class PcvrReadyService
{
    private readonly App _app;

    public PcvrReadyService(App app) => _app = app;

    public PcvrReadyReport Evaluate()
    {
        var items = new List<PcvrReadyItem>
        {
            CheckOvrService(),
            CheckOpenXrPreferred(),
            CheckSteamVrInstalled(),
            CheckSteamVrOpenXrAvailable(),
            CheckSteamLinkOpenXrAssist(),
            CheckPowerPlan(),
            CheckAudio(),
            CheckLinkApplyOnStart(),
            CheckGpu(),
            CheckDashToSteamVr()
        };

        return new PcvrReadyReport { Items = items };
    }

    public string Fix(string itemId) => itemId switch
    {
        "ovrservice" => FixOvrService(),
        "openxr" => FixOpenXrPreferred(),
        "steamvr-install" => _app.SteamVrInstall.OpenInstallPage(),
        "steamvr-openxr" => FixSteamVrOpenXr(),
        "steam-link-assist" => FixSteamLinkAssist(),
        "power" => FixPower(),
        "audio" => "Open Tray Tool → Audio switching and pick VR + fallback devices (or Capture fallback).",
        "link-on-start" => FixLinkOnStart(),
        "gpu" => _app.ApplyGpuRecommendedPresets(),
        "dash-steamvr" => FixDashToSteamVr(),
        _ => $"Unknown checklist item: {itemId}"
    };

    public string FixAll()
    {
        var report = Evaluate();
        var parts = new List<string>();
        foreach (var item in report.Items.Where(i => i.CanFix && i.Level != PcvrReadyLevel.Ok))
        {
            parts.Add($"{item.Title}: {Fix(item.Id)}");
        }

        return parts.Count == 0
            ? "Nothing to fix — checklist looks good."
            : string.Join(Environment.NewLine, parts);
    }

    private PcvrReadyItem CheckOvrService()
    {
        _app.Oculus.Refresh();
        if (!_app.Oculus.ServiceExists)
        {
            return Item("ovrservice", "OVRService", "Meta Quest PC runtime / OVRService not installed.",
                PcvrReadyLevel.Fail);
        }

        if (_app.Oculus.IsServiceRunning)
        {
            return Item("ovrservice", "OVRService", "Running — Link can start.", PcvrReadyLevel.Ok);
        }

        return Item("ovrservice", "OVRService", "Installed but not running.", PcvrReadyLevel.Fail,
            "Start OVRService", canFix: true);
    }

    private PcvrReadyItem CheckOpenXrPreferred()
    {
        var preferred = _app.Settings.Current.OpenXr.PreferredRuntime;
        if (preferred is OpenXrRuntimeKind.Inherit)
        {
            preferred = OpenXrRuntimeKind.Meta;
        }

        var active = _app.OpenXr.ReadActiveKind();
        var preferredLabel = OpenXrRuntimeService.Label(preferred);
        var activeLabel = OpenXrRuntimeService.Label(active);

        if (active == preferred)
        {
            var steamNote = preferred == OpenXrRuntimeKind.SteamVr
                ? " SteamVR OpenXR is active (good for Steam PCVR)."
                : " Tip: set Preferred to SteamVR if most games are SteamVR OpenXR titles.";
            return Item("openxr", "OpenXR runtime",
                $"Active matches preferred ({activeLabel}).{steamNote}",
                preferred == OpenXrRuntimeKind.SteamVr ? PcvrReadyLevel.Ok : PcvrReadyLevel.Warn,
                preferred == OpenXrRuntimeKind.SteamVr ? null : "Prefer SteamVR",
                canFix: preferred != OpenXrRuntimeKind.SteamVr);
        }

        return Item("openxr", "OpenXR runtime",
            $"Active is {activeLabel}; preferred is {preferredLabel}.",
            PcvrReadyLevel.Fail, "Apply preferred", canFix: true);
    }

    private PcvrReadyItem CheckSteamVrInstalled()
    {
        var info = _app.SteamVrInstall.Probe();
        if (!info.IsInstalled)
        {
            return Item("steamvr-install", "SteamVR installed",
                "SteamVR is not installed. Most Steam PCVR games need it.",
                PcvrReadyLevel.Fail, "Install SteamVR", canFix: true);
        }

        var level = info.Channel == SteamVrChannel.Beta ? PcvrReadyLevel.Warn : PcvrReadyLevel.Ok;
        var title = info.Channel == SteamVrChannel.Beta ? "SteamVR (Beta)" : "SteamVR installed";
        return Item("steamvr-install", title, info.Detail, level);
    }

    private PcvrReadyItem CheckSteamVrOpenXrAvailable()
    {
        if (_app.OpenXr.IsAvailable(OpenXrRuntimeKind.SteamVr))
        {
            return Item("steamvr-openxr", "SteamVR OpenXR",
                "SteamVR OpenXR runtime JSON found.", PcvrReadyLevel.Ok);
        }

        var install = _app.SteamVrInstall.Probe();
        if (!install.IsInstalled)
        {
            return Item("steamvr-openxr", "SteamVR OpenXR",
                "SteamVR not installed — OpenXR JSON unavailable.",
                PcvrReadyLevel.Fail, "Install SteamVR", canFix: true);
        }

        return Item("steamvr-openxr", "SteamVR OpenXR",
            "SteamVR is installed but steamxr_win64.json was not found — repair SteamVR in Steam.",
            PcvrReadyLevel.Fail, "Open SteamVR page", canFix: true);
    }

    private string FixSteamVrOpenXr() => _app.SteamVrInstall.OpenInstallPage();

    private PcvrReadyItem CheckSteamLinkOpenXrAssist()    {
        var on = _app.Settings.Current.OpenXr.PreferSteamVrDuringSteamLink;
        if (on)
        {
            return Item("steam-link-assist", "Steam Link OpenXR assist",
                "Will switch OpenXR to SteamVR during Steam Link / SteamVR sessions.",
                PcvrReadyLevel.Ok);
        }

        return Item("steam-link-assist", "Steam Link OpenXR assist",
            "Off — Steam Link sessions may keep Meta OpenXR and break SteamVR OpenXR games.",
            PcvrReadyLevel.Warn, "Enable assist", canFix: true);
    }

    private PcvrReadyItem CheckPowerPlan()
    {
        var power = _app.Settings.Current.Power;
        if (string.IsNullOrWhiteSpace(power.VrPlanGuid))
        {
            return Item("power", "VR power plan",
                "No VR power plan selected — set one under Power Options.",
                PcvrReadyLevel.Warn);
        }

        var active = _app.Power.GetActivePlan();
        var match = active is not null
                    && string.Equals(active.Guid.ToString("D"), power.VrPlanGuid, StringComparison.OrdinalIgnoreCase);
        if (match)
        {
            return Item("power", "VR power plan",
                $"Active plan is VR plan ({active!.Name}).", PcvrReadyLevel.Ok);
        }

        var label = power.AutoSwitchEnabled
            ? $"VR plan configured; auto-switch on. Active now: {active?.Name ?? "unknown"}."
            : $"VR plan configured but auto-switch is off. Active: {active?.Name ?? "unknown"}.";
        return Item("power", "VR power plan", label,
            power.AutoSwitchEnabled ? PcvrReadyLevel.Ok : PcvrReadyLevel.Warn,
            "Apply VR plan", canFix: true);
    }

    private PcvrReadyItem CheckAudio()
    {
        var audio = _app.Settings.Current.Audio;
        var hasVr = !string.IsNullOrWhiteSpace(audio.VrPlaybackDeviceId);
        var hasFallback = !string.IsNullOrWhiteSpace(audio.FallbackPlaybackDeviceId);

        if (!audio.AutoSwitchEnabled)
        {
            return Item("audio", "Audio switching",
                "Auto-switch is off — enable it under Tray Tool → Audio.",
                PcvrReadyLevel.Warn);
        }

        if (hasVr && hasFallback)
        {
            var vrName = _app.Audio.FindName(audio.VrPlaybackDeviceId) ?? "VR playback";
            return Item("audio", "Audio switching",
                $"Configured — VR: {vrName}; fallback set.", PcvrReadyLevel.Ok);
        }

        if (hasVr)
        {
            return Item("audio", "Audio switching",
                "VR playback set, but no fallback speakers — capture fallback recommended.",
                PcvrReadyLevel.Warn);
        }

        return Item("audio", "Audio switching",
            "VR playback device not configured — pick devices under Tray Tool → Audio.",
            PcvrReadyLevel.Warn);
    }

    private PcvrReadyItem CheckLinkApplyOnStart()
    {
        if (_app.Settings.Current.ApplyLinkSettingsOnStart)
        {
            var name = _app.Settings.Current.LinkSettings.PresetName;
            return Item("link-on-start", "Link settings on start",
                $"On — will re-apply Link preset “{name}” at tray start.", PcvrReadyLevel.Ok);
        }

        return Item("link-on-start", "Link settings on start",
            "Off — Meta may reset RemoteHeadset after client updates.",
            PcvrReadyLevel.Warn, "Enable + apply now", canFix: true);
    }

    private PcvrReadyItem CheckGpu()
    {
        var rec = _app.Gpu.GetRecommendation();
        if (rec is null)
        {
            return Item("gpu", "GPU presets", "No GPU detected.", PcvrReadyLevel.Warn);
        }

        var link = _app.Settings.Current.LinkSettings;
        var matches = string.Equals(link.PresetName, rec.LinkPresetName, StringComparison.OrdinalIgnoreCase)
                      || (link.BitrateMbps == rec.Link.BitrateMbps
                          && link.EncodeResolutionWidth == rec.Link.EncodeResolutionWidth);
        if (matches)
        {
            return Item("gpu", "GPU presets",
                $"{rec.Adapter.Name} — Link already matches recommended “{rec.LinkPresetName}”.",
                PcvrReadyLevel.Ok);
        }

        return Item("gpu", "GPU presets",
            $"{rec.Adapter.Summary} — recommended Link “{rec.LinkPresetName}”.",
            PcvrReadyLevel.Warn, "Apply GPU presets", canFix: true);
    }

    private PcvrReadyItem CheckDashToSteamVr()
    {
        var dash = _app.Settings.Current.DashToSteamVr;
        var prevent = _app.DashToSteamVr.IsPreventDashLaunchEnabled() || dash.PreferPreventDashLaunch;
        var steamVrRunning = IsSteamVrRunning();
        var auto = dash.AutoOnMetaLinkConnect || prevent;

        if (steamVrRunning)
        {
            return Item("dash-steamvr", "Dash → SteamVR",
                "SteamVR (vrserver) is running.", PcvrReadyLevel.Ok);
        }

        if (auto)
        {
            return Item("dash-steamvr", "Dash → SteamVR",
                prevent
                    ? "PreventDashLaunch / auto SteamVR on Meta Link is configured."
                    : "Auto Dash → SteamVR on Meta Link connect is on.",
                PcvrReadyLevel.Ok);
        }

        return Item("dash-steamvr", "Dash → SteamVR",
            "Off — for Steam PCVR over Meta Link, enable auto Dash→SteamVR or PreventDashLaunch.",
            PcvrReadyLevel.Warn, "Enable auto on Link", canFix: true);
    }

    private string FixOvrService()
    {
        _app.Oculus.Refresh();
        return _app.Oculus.IsServiceRunning ? "OVRService already running." : _app.Oculus.Start();
    }

    private string FixOpenXrPreferred()
    {
        var settings = _app.Settings.Current.OpenXr;
        // Steam-first: if they clicked fix from the Meta-preferred warn, switch preferred to SteamVR.
        if (settings.PreferredRuntime != OpenXrRuntimeKind.SteamVr
            && _app.OpenXr.ReadActiveKind() == settings.PreferredRuntime)
        {
            settings.PreferredRuntime = OpenXrRuntimeKind.SteamVr;
            settings.ApplyOnStart = true;
            _app.Settings.Save();
        }

        var target = settings.PreferredRuntime is OpenXrRuntimeKind.SteamVr or OpenXrRuntimeKind.Meta
            ? settings.PreferredRuntime
            : OpenXrRuntimeKind.SteamVr;
        settings.PreferredRuntime = target;
        _app.Settings.Save();
        return _app.OpenXr.Set(target);
    }

    private string FixSteamLinkAssist()
    {
        _app.Settings.Current.OpenXr.PreferSteamVrDuringSteamLink = true;
        _app.Settings.Save();
        return "Steam Link OpenXR assist enabled.";
    }

    private string FixPower()
    {
        var power = _app.Settings.Current.Power;
        if (string.IsNullOrWhiteSpace(power.VrPlanGuid))
        {
            return "Pick a VR power plan under Power Options first.";
        }

        power.AutoSwitchEnabled = true;
        _app.Settings.Save();
        return _app.Power.ApplyVrPlan(power);
    }

    private string FixLinkOnStart()
    {
        _app.Settings.Current.ApplyLinkSettingsOnStart = true;
        _app.Settings.Save();
        var apply = _app.ApplyMetaLinkSettings(_app.Settings.Current.LinkSettings, deleteUnsetOverrides: true);
        return "Link apply-on-start enabled. " + apply;
    }

    private string FixDashToSteamVr()
    {
        var dash = _app.Settings.Current.DashToSteamVr;
        dash.AutoOnMetaLinkConnect = true;
        dash.SwitchOpenXrToSteamVr = true;
        _app.Settings.Save();
        return "Auto Dash → SteamVR on Meta Link connect enabled (OpenXR → SteamVR).";
    }

    private static bool IsSteamVrRunning()
    {
        try
        {
            return Process.GetProcessesByName("vrserver").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static PcvrReadyItem Item(
        string id,
        string title,
        string detail,
        PcvrReadyLevel level,
        string? fixLabel = null,
        bool canFix = false) => new()
    {
        Id = id,
        Title = title,
        Detail = detail,
        Level = level,
        FixLabel = fixLabel,
        CanFix = canFix
    };
}
