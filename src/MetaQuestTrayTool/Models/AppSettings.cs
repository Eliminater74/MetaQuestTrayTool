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
    public bool ApplyGameSettingsOnStart { get; set; }
    public bool AutoApplyProfiles { get; set; } = true;
    public bool ApplyLinkSettingsOnStart { get; set; }
    public TrayToolSettings Tray { get; set; } = new();
    public ServiceStartupSettings Service { get; set; } = new();
    public GameSettings DefaultGameSettings { get; set; } = new();
    public LinkSettings LinkSettings { get; set; } = new();
    public OpenXrSettings OpenXr { get; set; } = new();
    public AudioSwitchSettings Audio { get; set; } = new() { AutoSwitchEnabled = true };
    public PowerSettings Power { get; set; } = new();
    public HeadsetSettings Headset { get; set; } = new();
    public List<GameProfile> Profiles { get; set; } = [];
}
