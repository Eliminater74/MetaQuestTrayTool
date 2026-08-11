namespace MetaQuestTrayTool.Models;

/// <summary>
/// Per-app settings applied when a VR game or experience is detected.
/// </summary>
public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public GamePlatform Platform { get; set; } = GamePlatform.Custom;
    public ProfileScope Scope { get; set; } = ProfileScope.Personal;
    public string? AppId { get; set; }
    public string? InstallPath { get; set; }
    public GameSettings Settings { get; set; } = new();
    public string CpuPriority { get; set; } = "Normal";
    public string? Comments { get; set; }
}
