using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Resolves Steam librarycache / CDN headers and Meta StoreAssets covers.
/// Local files first (offline), then a one-time CDN download into AppData.
/// </summary>
public sealed class LibraryArtworkService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MetaQuestTrayTool/0.7");
        return client;
    }

    private static readonly string[] SteamFileNames =
    [
        "header.jpg",
        "library_600x900.jpg",
        "logo.png",
        "icon.jpg"
    ];

    private static readonly string[] MetaFileNames =
    [
        "cover_landscape_image.jpg",
        "cover_landscape_image.png",
        "small_landscape_image.jpg",
        "cover_square_image.jpg",
        "icon_image.jpg",
        "cover_landscape_image_large.png"
    ];

    private static readonly string[] SteamCdnTemplates =
    [
        "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg",
        "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg",
        "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/header.jpg",
        "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/capsule_231x87.jpg"
    ];

    public string? FindSteamLocal(string steamRoot, string appId)
    {
        var nested = Path.Combine(steamRoot, "appcache", "librarycache", appId);
        if (Directory.Exists(nested))
        {
            foreach (var name in SteamFileNames)
            {
                var file = Path.Combine(nested, name);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            var fallback = Directory.EnumerateFiles(nested, "*.jpg")
                .Concat(Directory.EnumerateFiles(nested, "*.png"))
                .FirstOrDefault(path =>
                    !path.Contains("blur", StringComparison.OrdinalIgnoreCase)
                    && new FileInfo(path).Length > 2048);
            if (fallback is not null)
            {
                return fallback;
            }
        }

        var flat = Path.Combine(steamRoot, "appcache", "librarycache");
        foreach (var name in new[] { $"{appId}_header.jpg", $"{appId}_library_600x900.jpg", $"{appId}_icon.jpg" })
        {
            var file = Path.Combine(flat, name);
            if (File.Exists(file))
            {
                return file;
            }
        }

        var cached = CachedSteamPath(appId);
        return File.Exists(cached) ? cached : null;
    }

    public string SteamCdnUrl(string appId) => string.Format(SteamCdnTemplates[0], appId);

    public string? FindMetaLocal(string canonical, IEnumerable<string> storeAssetRoots)
    {
        var folderName = canonical.EndsWith("_assets", StringComparison.OrdinalIgnoreCase)
            ? canonical
            : canonical + "_assets";

        foreach (var root in storeAssetRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var dir = Path.Combine(root, folderName);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var name in MetaFileNames)
            {
                var file = Path.Combine(dir, name);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            var any = Directory.EnumerateFiles(dir, "*.jpg")
                .Concat(Directory.EnumerateFiles(dir, "*.png"))
                .FirstOrDefault();
            if (any is not null)
            {
                return any;
            }
        }

        return null;
    }

    public string? FindExeIcon(string? installPath, string? launchFile, string cacheKey)
    {
        var exe = ResolveExe(installPath, launchFile);
        if (exe is null || !File.Exists(exe))
        {
            return null;
        }

        AppPaths.EnsureAppDataDirectory();
        var dest = Path.Combine(AppPaths.ArtworkDirectory, $"{cacheKey}_icon.png");
        if (File.Exists(dest) && new FileInfo(dest).Length > 0)
        {
            return dest;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exe);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(dest, ImageFormat.Png);
            return dest;
        }
        catch
        {
            return null;
        }
    }

    public string? ResolveForProfile(GameProfile profile, string? steamRoot, IEnumerable<string> storeAssetRoots)
    {
        if (profile.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(profile.AppId))
        {
            if (!string.IsNullOrWhiteSpace(steamRoot))
            {
                var local = FindSteamLocal(steamRoot, profile.AppId);
                if (local is not null)
                {
                    return local;
                }
            }

            var cached = CachedSteamPath(profile.AppId);
            if (File.Exists(cached))
            {
                return cached;
            }
        }

        if (profile.Platform == GamePlatform.Meta && !string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            var canonical = Path.GetFileName(profile.InstallPath.TrimEnd('\\', '/'));
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                var meta = FindMetaLocal(canonical, storeAssetRoots);
                if (meta is not null)
                {
                    return meta;
                }
            }
        }

        return FindExeIcon(
            profile.InstallPath,
            null,
            $"{profile.Platform}_{profile.AppId ?? profile.ProcessName}");
    }

    public async Task PrefetchSteamAsync(
        IReadOnlyList<LibraryGame> games,
        Action<LibraryGame, string>? apply,
        CancellationToken cancellationToken)
    {
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.Platform != GamePlatform.Steam
                || string.IsNullOrWhiteSpace(game.AppId)
                || !string.IsNullOrWhiteSpace(game.ArtworkPath))
            {
                continue;
            }

            var cached = CachedSteamPath(game.AppId);
            if (File.Exists(cached))
            {
                (apply ?? AssignPath)(game, cached);
                continue;
            }

            var downloaded = await DownloadSteamAsync(game.AppId, cached, cancellationToken).ConfigureAwait(false);
            if (downloaded is not null)
            {
                (apply ?? AssignPath)(game, downloaded);
            }
        }
    }

    private static void AssignPath(LibraryGame game, string path) => game.ArtworkPath = path;

    private static async Task<string?> DownloadSteamAsync(string appId, string dest, CancellationToken cancellationToken)
    {
        AppPaths.EnsureAppDataDirectory();
        foreach (var template in SteamCdnTemplates)
        {
            try
            {
                using var response = await Http.GetAsync(
                    new Uri(string.Format(template, appId)),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (type.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(dest);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                if (new FileInfo(dest).Length > 512)
                {
                    return dest;
                }

                File.Delete(dest);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // try next CDN host
            }
        }

        return null;
    }

    private static string CachedSteamPath(string appId) =>
        Path.Combine(AppPaths.ArtworkDirectory, $"steam_{appId}_header.jpg");

    private static string? ResolveExe(string? installPath, string? launchFile)
    {
        if (!string.IsNullOrWhiteSpace(installPath) && !string.IsNullOrWhiteSpace(launchFile))
        {
            var combined = Path.IsPathRooted(launchFile)
                ? launchFile
                : Path.Combine(installPath, launchFile);
            if (File.Exists(combined))
            {
                return combined;
            }
        }

        if (!string.IsNullOrWhiteSpace(launchFile) && File.Exists(launchFile))
        {
            return launchFile;
        }

        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return null;
        }

        try
        {
            return Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
