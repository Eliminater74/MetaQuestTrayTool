namespace MetaQuestTrayTool.Models;

public enum ProfilePresetKind
{
    Global,
    Game,
    GameTemplate
}

/// <summary>Built-in starting point for global defaults or a personal profile. Not stored in the user profile file.</summary>
public sealed class ProfilePreset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public ProfilePresetKind Kind { get; init; }
    public string? ProcessName { get; init; }
    public GameSettings Settings { get; init; } = new();
    public LinkProfileOverrides Link { get; init; } = new();
    public LinkSettings? GlobalLink { get; init; }
    public OpenXrRuntimeKind OpenXrRuntime { get; init; } = OpenXrRuntimeKind.Inherit;
    public string CpuPriority { get; init; } = "Normal";
}
