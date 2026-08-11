namespace MetaQuestTrayTool.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool ApplyGameSettingsOnStart { get; set; }
    public GameSettings DefaultGameSettings { get; set; } = new();
    public List<GameProfile> Profiles { get; set; } = [];
}
