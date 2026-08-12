namespace MetaQuestTrayTool.Models;

/// <summary>
/// Air Link / wired Link → kill Meta Dash → start SteamVR (OculusKiller-style, without replacing OculusDash.exe).
/// Inspired by https://github.com/DevOculus-Meta-Quest/OculusKiller
/// </summary>
public sealed class DashToSteamVrSettings
{
    /// <summary>When Meta Air Link / wired Link becomes an active session, run kill-dash + SteamVR once.</summary>
    public bool AutoOnMetaLinkConnect { get; set; }

    /// <summary>Also set OpenXR ActiveRuntime to SteamVR when running the action.</summary>
    public bool SwitchOpenXrToSteamVr { get; set; } = true;

    /// <summary>While SteamVR (vrserver) is running, keep terminating OculusDash if Meta respawns it.</summary>
    public bool KeepKillingDashWhileSteamVr { get; set; } = true;

    /// <summary>Also close Meta Horizon Link / Oculus client windows (not OVRService).</summary>
    public bool CloseMetaClient { get; set; }

    /// <summary>
    /// Preferred state for HKLM …\Oculus\Config\PreventDashLaunch (DWORD 1 = never launch Dash).
    /// When enabled, the tray also auto-starts SteamVR on Meta Link connect (registry alone does not).
    /// </summary>
    public bool PreferPreventDashLaunch { get; set; }
}
