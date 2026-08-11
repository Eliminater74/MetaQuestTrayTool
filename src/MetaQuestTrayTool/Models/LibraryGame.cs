namespace MetaQuestTrayTool.Models;

public enum GamePlatform
{
    Custom,
    Steam,
    Meta
}

public enum ProfileScope
{
    /// <summary>Per-app override for one game/process.</summary>
    Personal,

    /// <summary>Reserved for shared/global catalogue entries; defaults live in DefaultGameSettings.</summary>
    Global
}

public sealed class LibraryGame
{
    public required string Name { get; init; }
    public required GamePlatform Platform { get; init; }
    public string? AppId { get; init; }
    public string? InstallPath { get; init; }
    public string? LaunchFile { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public bool IsThirdParty { get; init; }

    public string PlatformLabel => Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Meta => IsThirdParty ? "Meta (3rd party)" : "Meta",
        _ => "Custom"
    };

    public override string ToString() => $"{Name}  ·  {PlatformLabel}  ·  {ProcessName}";
}
