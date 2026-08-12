using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class GameLibraryService
{
    private readonly SteamLibraryService _steam = new();
    private readonly MetaLibraryService _meta = new();
    public LibraryArtworkService Artwork { get; } = new();

    public IReadOnlyList<string> GetMetaStoreAssetRoots() => _meta.GetStoreAssetRoots();
    public string? DetectSteamRoot() => _steam.DetectSteamRoot();

    public IReadOnlyList<LibraryGame> GetAllGames()
    {
        return _steam.GetInstalledGames()
            .Concat(_meta.GetInstalledGames())
            .OrderBy(game => game.Platform)
            .ThenBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<LibraryGame> GetSteamGames() => _steam.GetInstalledGames();
    public IReadOnlyList<LibraryGame> GetMetaGames() => _meta.GetInstalledGames();
}
