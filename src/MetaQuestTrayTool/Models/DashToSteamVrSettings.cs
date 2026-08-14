namespace MetaQuestTrayTool.Models;

/// <summary>
/// Air Link / wired Link → PreventDashLaunch registry + start SteamVR (OculusKiller-style).
/// Does not kill Meta processes — Dash is blocked via
/// <c>HKLM\…\Oculus\Config\PreventDashLaunch</c> only.
/// Inspired by https://github.com/DevOculus-Meta-Quest/OculusKiller
/// </summary>
public sealed class DashToSteamVrSettings
{
    /// <summary>Also set OpenXR ActiveRuntime to SteamVR when starting SteamVR over Link.</summary>
    public bool SwitchOpenXrToSteamVr { get; set; } = true;

    /// <summary>
    /// Preferred state for HKLM …\Oculus\Config\PreventDashLaunch (DWORD 1 = never launch Dash).
    /// When enabled, the tray also auto-starts SteamVR on Meta Link connect (registry alone does not).
    /// </summary>
    public bool PreferPreventDashLaunch { get; set; }

    /// <summary>
    /// Optional: when enabling PreventDashLaunch, also set CoreChannel to NO_UPDATES
    /// (OculusKiller precaution so Meta does not overwrite Dash/runtime tweaks). Off by default.
    /// </summary>
    public bool AlsoSetNoUpdatesWithPreventDash { get; set; }

    /// <summary>
    /// After SteamVR exits, restart OVRService so Meta Link drops and the headset can return to Quest Home.
    /// </summary>
    public bool RestartOvrServiceWhenSteamVrExits { get; set; } = true;

    /// <summary>Last CoreChannel before we switched to NO_UPDATES (so Stable / PublicTest can be restored).</summary>
    public string? CoreChannelBeforeNoUpdates { get; set; }
}
