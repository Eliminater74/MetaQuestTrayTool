using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class MetaLibraryService
{
    public IReadOnlyList<string> GetLibraryRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Oculus VR, LLC\Oculus\Libraries");
            if (key is not null)
            {
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    if (sub?.GetValue("OriginalPath") is string path && Directory.Exists(path))
                    {
                        roots.Add(path.TrimEnd('\\'));
                    }
                }
            }
        }
        catch
        {
            // Fall through to defaults.
        }

        if (Directory.Exists(@"C:\Program Files\Oculus"))
        {
            roots.Add(@"C:\Program Files\Oculus");
        }

        return roots.ToList();
    }

    public IReadOnlyList<LibraryGame> GetInstalledGames()
    {
        var games = new Dictionary<string, LibraryGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetLibraryRoots())
        {
            var manifests = System.IO.Path.Combine(root, "Manifests");
            if (!Directory.Exists(manifests))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(manifests, "*.json"))
            {
                if (file.Contains("_assets", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    var rootElement = doc.RootElement;
                    if (!rootElement.TryGetProperty("packageType", out var packageType)
                        || !string.Equals(packageType.GetString(), "APP", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var canonical = rootElement.TryGetProperty("canonicalName", out var canonicalName)
                        ? canonicalName.GetString()
                        : null;
                    var launchFile = rootElement.TryGetProperty("launchFile", out var launch)
                        ? launch.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(launchFile))
                    {
                        continue;
                    }

                    // Skip obvious non-VR desktop shortcuts that sometimes appear in CoreData.
                    if (canonical.StartsWith("C_ProgramFiles_", StringComparison.OrdinalIgnoreCase)
                        || canonical.StartsWith("C_Users_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var appId = rootElement.TryGetProperty("appId", out var id) ? id.GetString() : null;
                    var thirdParty = rootElement.TryGetProperty("thirdParty", out var third) && third.ValueKind is JsonValueKind.True;
                    var process = System.IO.Path.GetFileNameWithoutExtension(launchFile) ?? string.Empty;
                    var installPath = System.IO.Path.Combine(root, "Software", canonical);
                    var display = HumanizeCanonicalName(canonical);

                    var game = new LibraryGame
                    {
                        Name = display,
                        Platform = GamePlatform.Meta,
                        AppId = appId,
                        InstallPath = Directory.Exists(installPath) ? installPath : null,
                        LaunchFile = System.IO.Path.GetFileName(launchFile),
                        ProcessName = process,
                        IsThirdParty = thirdParty
                    };

                    var key = appId ?? canonical;
                    games[key] = game;
                }
                catch
                {
                    // Skip bad manifests.
                }
            }
        }

        return games.Values
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string HumanizeCanonicalName(string canonical)
    {
        var name = canonical;
        var dash = canonical.IndexOf('-');
        if (dash > 0 && dash < canonical.Length - 1)
        {
            name = canonical[(dash + 1)..];
        }

        return name.Replace('-', ' ').Replace('_', ' ');
    }
}
