using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>How the user configured PCVR — drives expected OpenXR and status wording.</summary>
public enum PcvrSetupMode
{
    /// <summary>PreventDashLaunch + switch OpenXR to SteamVR — no Meta Dash, Steam PCVR over Link.</summary>
    SteamVrOverMetaLink,

    /// <summary>Standard Meta Link / Dash path — Meta OpenXR unless the user chose otherwise.</summary>
    MetaLink
}

/// <summary>Resolves the user's PCVR path so Status / PCVR Ready judge against setup, not stale saved prefs.</summary>
public static class PcvrSetup
{
    public static PcvrSetupMode GetMode(App app)
    {
        var dash = app.Settings.Current.DashToSteamVr;
        if (dash.SwitchOpenXrToSteamVr
            && (app.DashToSteamVr.IsPreventDashLaunchEnabled() || dash.PreferPreventDashLaunch))
        {
            return PcvrSetupMode.SteamVrOverMetaLink;
        }

        return PcvrSetupMode.MetaLink;
    }

    public static string ModeLabel(PcvrSetupMode mode) => mode switch
    {
        PcvrSetupMode.SteamVrOverMetaLink => "SteamVR over Link (PreventDash)",
        _ => "Meta Link / Dash"
    };

    public static OpenXrRuntimeKind ExpectedOpenXr(App app) => GetMode(app) switch
    {
        PcvrSetupMode.SteamVrOverMetaLink => OpenXrRuntimeKind.SteamVr,
        _ => NormalizePreferred(app.Settings.Current.OpenXr.PreferredRuntime)
    };

    public static OpenXrAlignment EvaluateOpenXr(App app, OpenXrRuntimeKind? active)
    {
        var mode = GetMode(app);
        var expected = ExpectedOpenXr(app);
        var setup = ModeLabel(mode);
        var expectedLabel = OpenXrRuntimeService.Label(expected);
        var activeLabel = OpenXrRuntimeService.Label(active);

        if (active == expected)
        {
            return new OpenXrAlignment(
                PcvrReadyLevel.Ok,
                $"Setup: {setup}. OpenXR is {activeLabel} (expected for this path).",
                null,
                false);
        }

        if (active is null)
        {
            return new OpenXrAlignment(
                mode == PcvrSetupMode.SteamVrOverMetaLink ? PcvrReadyLevel.Warn : PcvrReadyLevel.Fail,
                $"Setup: {setup}. No ActiveRuntime set — expected {expectedLabel}.",
                "Switch now",
                true);
        }

        if (mode == PcvrSetupMode.SteamVrOverMetaLink
            && app.Settings.Current.DashToSteamVr.SwitchOpenXrToSteamVr)
        {
            return new OpenXrAlignment(
                PcvrReadyLevel.Warn,
                $"Setup: {setup}. Active is {activeLabel} — will switch to SteamVR on Link connect or Start SteamVR.",
                "Switch to SteamVR",
                true);
        }

        return new OpenXrAlignment(
            PcvrReadyLevel.Fail,
            $"Setup: {setup}. Active is {activeLabel}; expected {expectedLabel}.",
            "Switch now",
            true);
    }

    /// <summary>Keep saved OpenXR prefs aligned with a SteamVR-over-Link setup (does not touch HKLM until Apply on start / connect).</summary>
    public static string? SyncSavedOpenXrToSetup(App app)
    {
        if (GetMode(app) != PcvrSetupMode.SteamVrOverMetaLink)
        {
            return null;
        }

        var openXr = app.Settings.Current.OpenXr;
        var changed = false;
        if (openXr.PreferredRuntime != OpenXrRuntimeKind.SteamVr)
        {
            openXr.PreferredRuntime = OpenXrRuntimeKind.SteamVr;
            changed = true;
        }

        if (!openXr.ApplyOnStart)
        {
            openXr.ApplyOnStart = true;
            changed = true;
        }

        if (!changed)
        {
            return null;
        }

        app.Settings.Save();
        return "Saved OpenXR preference aligned with SteamVR-over-Link setup (SteamVR, apply on start).";
    }

    public static StatusChipKind ChipKindFor(PcvrReadyLevel level) => level switch
    {
        PcvrReadyLevel.Ok => StatusChipKind.On,
        PcvrReadyLevel.Warn => StatusChipKind.Warn,
        _ => StatusChipKind.Fail
    };

    private static OpenXrRuntimeKind NormalizePreferred(OpenXrRuntimeKind preferred) =>
        preferred is OpenXrRuntimeKind.Inherit ? OpenXrRuntimeKind.Meta : preferred;
}

public readonly record struct OpenXrAlignment(
    PcvrReadyLevel Level,
    string Detail,
    string? FixLabel,
    bool CanFix);
