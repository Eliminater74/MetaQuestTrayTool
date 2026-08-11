namespace MetaQuestTrayTool.Models;

/// <summary>
/// Per-app settings applied when a VR game or experience is detected.
/// Process watching lands in Phase 2; the shape is stored now.
/// </summary>
public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public GameSettings Settings { get; set; } = new();
    public string CpuPriority { get; set; } = "Normal";
    public string? Comments { get; set; }
}
