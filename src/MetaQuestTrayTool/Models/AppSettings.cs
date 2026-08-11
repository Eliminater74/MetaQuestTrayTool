namespace MetaQuestTrayTool.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public double DefaultSuperSampling { get; set; } = 1.0;
    public string DefaultAswMode { get; set; } = "Auto";
    public List<GameProfile> Profiles { get; set; } = [];
}
