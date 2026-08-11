namespace MetaQuestTrayTool.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool ApplyGameSettingsOnStart { get; set; }
    public bool AutoApplyProfiles { get; set; } = true;
    public bool ApplyLinkSettingsOnStart { get; set; }
    public TrayToolSettings Tray { get; set; } = new();
    public ServiceStartupSettings Service { get; set; } = new();
    public GameSettings DefaultGameSettings { get; set; } = new();
    public LinkSettings LinkSettings { get; set; } = new();
    public AudioSwitchSettings Audio { get; set; } = new() { AutoSwitchEnabled = true };
    public PowerSettings Power { get; set; } = new();
    public List<GameProfile> Profiles { get; set; } = [];
}
