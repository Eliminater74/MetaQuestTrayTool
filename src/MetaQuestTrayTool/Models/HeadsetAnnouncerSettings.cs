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

    /// <summary>Results from hotkeys, voice commands, OpenXR changes, and manual tray actions.</summary>
    public bool ActionResults { get; set; } = true;

    /// <summary>Manual audio routing results. Automatic routing is summarized by session events.</summary>
    public bool Audio { get; set; } = true;

    /// <summary>Headset ADB, trust, and performance-tweak results.</summary>
    public bool Headset { get; set; } = true;

    /// <summary>PCVR recovery results.</summary>
    public bool Recovery { get; set; } = true;

    /// <summary>
    /// When a game profile is active, skip lower-priority action chatter —
    /// still allow session connect/disconnect, profile apply/restore, and the
    /// SteamVR-exit 10s OVR wait.
    /// </summary>
    public bool QuietWhileGameProfileActive { get; set; } = true;

    /// <summary>Wait for Link audio / auto-switch before speaking (milliseconds).</summary>
    public int DelayMs { get; set; } = 900;

    /// <summary>Extra wait after Link connect before the first spoken phrase (milliseconds).</summary>
    public int ConnectDelayMs { get; set; } = 2200;

    /// <summary>
    /// Windows TTS voice name. Empty = auto, preferring an English female voice (Zira when present).
    /// </summary>
    public string VoiceName { get; set; } = "";

    public HeadsetAnnouncerSettings Clone() => new()
    {
        Enabled = Enabled,
        SessionConnect = SessionConnect,
        SessionDisconnect = SessionDisconnect,
        Profiles = Profiles,
        GameLaunch = GameLaunch,
        DashToSteamVr = DashToSteamVr,
        SteamLinkAssist = SteamLinkAssist,
        ActionResults = ActionResults,
        Audio = Audio,
        Headset = Headset,
        Recovery = Recovery,
        QuietWhileGameProfileActive = QuietWhileGameProfileActive,
        DelayMs = DelayMs,
        ConnectDelayMs = ConnectDelayMs,
        VoiceName = VoiceName
    };
}

public enum HeadsetAnnounceKind
{
    SessionConnect,
    SessionDisconnect,
    ProfileApplied,
    ProfileRestored,
    GameLaunch,
    LaunchFailed,
    DashToSteamVr,
    SteamVrExit,
    SteamLinkAssist,
    GlobalDefaults,
    ActionResult,
    Audio,
    Headset,
    Recovery
}
