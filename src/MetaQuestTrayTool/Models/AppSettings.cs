namespace MetaQuestTrayTool.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartWithWindowsAsAdministrator { get; set; }

    /// <summary>
    /// Set-and-forget: the tray restarts elevated once, then starts itself at logon
    /// with Administrator rights so OpenXR / OVRService / profiles never need a
    /// UAC click while the headset is on. Uncheck to opt out.
    /// </summary>
    public bool AutomaticElevation { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Push global Debug Tool + Link + OpenXR when the tool starts (if no game profile is active).</summary>
    public bool ApplyGameSettingsOnStart { get; set; } = true;

    /// <summary>Watch running games and swap to a personal profile, then restore global when the game exits.</summary>
    public bool AutoApplyProfiles { get; set; } = true;

    /// <summary>
    /// Process names (no .exe) that must never trigger auto-apply — Discord, browsers, overlays, etc.
    /// </summary>
    public List<string> ProfileIgnoreProcesses { get; set; } =
    [
        "discord",
        "chrome",
        "msedge",
        "firefox",
        "steam",
        "steamwebhelper",
        "steamvr",
        "vrserver",
        "vrdashboard",
        "ovrserver_x64",
        "oculusclient",
        "oculusdash",
        "explorer",
        "searchhost",
        "cursor",
        "code",
        "devenv"
    ];

    /// <summary>
    /// Optional: when a PCVR Link/SteamVR session connects, quietly end these overlay / RGB / OSD processes.
    /// Process names without .exe. Empty = disabled.
    /// </summary>
    public bool CloseOverlaysOnLinkConnect { get; set; }

    public List<string> OverlayCloseProcesses { get; set; } =
    [
        "rtss",
        "encoder_server64",
        "msi afterburner",
        "afterburner",
        "rivatuner",
        "hwinfo64",
        "hwinfo32",
        "nzxt cam",
        "cam",
        "icue",
        "lghub",
        "armourycrate",
        "wallpaper64",
        "wallpaper32"
    ];

    /// <summary>Include Quest Link registry values in the global baseline.</summary>
    public bool ApplyLinkSettingsOnStart { get; set; } = true;

    /// <summary>Re-apply global defaults when a VR headset connects over ADB (unless a game profile is active).</summary>
    public bool ApplyGlobalWhenHeadsetConnects { get; set; } = true;
    public TrayToolSettings Tray { get; set; } = new();
    public HotKeySettings HotKeys { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public HeadsetAnnouncerSettings HeadsetAnnouncer { get; set; } = new();
    public ServiceStartupSettings Service { get; set; } = new();
    public GameSettings DefaultGameSettings { get; set; } = new();
    public LinkSettings LinkSettings { get; set; } = new();
    public OpenXrSettings OpenXr { get; set; } = new();
    public DashToSteamVrSettings DashToSteamVr { get; set; } = new();
    public AudioSwitchSettings Audio { get; set; } = new() { AutoSwitchEnabled = true };
    public PowerSettings Power { get; set; } = new();
    public HeadsetSettings Headset { get; set; } = new();
    private CustomCommandSet _customCommands = new();
    public CustomCommandSet CustomCommands
    {
        get => _customCommands;
        set => _customCommands = value ?? new();
    }
    public List<GameProfile> Profiles { get; set; } = [];
}
