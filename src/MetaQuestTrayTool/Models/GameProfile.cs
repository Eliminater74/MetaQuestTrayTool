namespace MetaQuestTrayTool.Models;

/// <summary>
/// Per-app settings applied when a VR game or experience is detected.
/// This is the data shape used by the original Oculus Tray Tool profiles.
/// </summary>
public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public double SuperSampling { get; set; } = 1.0;
    public string AswMode { get; set; } = "Inherit";
    public string CpuPriority { get; set; } = "Normal";
    public double FovMultiplier { get; set; } = 1.0;
    public string? Comments { get; set; }
}
