namespace MetaQuestTrayTool.Models;

/// <summary>
/// Spoken status in the Quest headset while PCVR is active — parallel to desktop tray notifications.
/// </summary>
public sealed class HeadsetAnnouncerSettings
{
    public bool Enabled { get; set; }

    /// <summary>Speak when Air Link, wired Link, Steam Link, or Virtual Desktop connects.</summary>
    public bool SessionConnect { get; set; } = true;

    /// <summary>Speak when the active PCVR session ends.</summary>
    public bool SessionDisconnect { get; set; } = true;

    /// <summary>Auto-apply personal profile or restore global defaults.</summary>
    public bool Profiles { get; set; } = true;

    /// <summary>Launch from library / profile picker.</summary>
    public bool GameLaunch { get; set; } = true;

    /// <summary>SteamVR start / PreventDashLaunch → SteamVR, and OVRService restart on SteamVR exit.</summary>
    public bool DashToSteamVr { get; set; } = true;

    /// <summary>Steam Link OpenXR nudge / restore (informational).</summary>
    public bool SteamLinkAssist { get; set; }

    /// <summary>
    /// When a game profile is active, skip profile/launch/Dash/Steam Link phrases —
    /// still allow session connect/disconnect.
    /// </summary>
    public bool QuietWhileGameProfileActive { get; set; } = true;

    /// <summary>Wait for Link audio / auto-switch before speaking (milliseconds).</summary>
    public int DelayMs { get; set; } = 900;

    public HeadsetAnnouncerSettings Clone() => new()
    {
        Enabled = Enabled,
        SessionConnect = SessionConnect,
        SessionDisconnect = SessionDisconnect,
        Profiles = Profiles,
        GameLaunch = GameLaunch,
        DashToSteamVr = DashToSteamVr,
        SteamLinkAssist = SteamLinkAssist,
        QuietWhileGameProfileActive = QuietWhileGameProfileActive,
        DelayMs = DelayMs
    };
}

public enum HeadsetAnnounceKind
{
    SessionConnect,
    SessionDisconnect,
    ProfileApplied,
    ProfileRestored,
    GameLaunch,
    DashToSteamVr,
    SteamVrExit,
    SteamLinkAssist,
    GlobalDefaults
}
