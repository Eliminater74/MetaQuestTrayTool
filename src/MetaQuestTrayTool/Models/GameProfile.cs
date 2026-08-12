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

    private LinkProfileOverrides _link = new();
    public LinkProfileOverrides Link
    {
        get => _link;
        set => _link = value ?? new();
    }

    public OpenXrRuntimeKind OpenXrRuntime { get; set; } = OpenXrRuntimeKind.Inherit;
    public string CpuPriority { get; set; } = "Normal";
    public string? Comments { get; set; }
    private CustomCommandSet _customCommands = new();
    public CustomCommandSet CustomCommands
    {
        get => _customCommands;
        set => _customCommands = value ?? new();
    }
}
