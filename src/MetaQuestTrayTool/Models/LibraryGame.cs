using System.ComponentModel;
using System.Runtime.CompilerServices;

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

public sealed class LibraryGame : INotifyPropertyChanged
{
    private string? _artworkPath;

    public required string Name { get; init; }
    public required GamePlatform Platform { get; init; }
    public string? AppId { get; init; }
    public string? InstallPath { get; init; }
    public string? LaunchFile { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public bool IsThirdParty { get; init; }
    public string? CanonicalName { get; init; }
    public string? ArtworkUrl { get; init; }

    public string? ArtworkPath
    {
        get => _artworkPath;
        set
        {
            if (_artworkPath == value)
            {
                return;
            }

            _artworkPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Artwork));
        }
    }

    public string? Artwork => ArtworkPath ?? ArtworkUrl;

    public string PlatformLabel => Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Meta => IsThirdParty ? "Meta (3rd party)" : "Meta",
        _ => "Custom"
    };

    public string PlatformGlyph => Platform == GamePlatform.Steam ? "S" : Platform == GamePlatform.Meta ? "M" : "•";

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => $"{Name}  ·  {PlatformLabel}  ·  {ProcessName}";

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
