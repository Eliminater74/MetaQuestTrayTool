using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class SteamLibraryService
{
    private static readonly Regex PathRegex = new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AppIdRegex = new("\"appid\"\\s*\"(\\d+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NameRegex = new("\"name\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirRegex = new("\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? DetectSteamRoot()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            if (key?.GetValue("SteamPath") is string steamPath && Directory.Exists(steamPath))
            {
                return steamPath.Replace('/', '\\');
            }
        }

        foreach (var path in new[]
                 {
                     @"C:\Program Files (x86)\Steam",
                     @"C:\Program Files\Steam"
                 })
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public IReadOnlyList<LibraryGame> GetInstalledGames()
    {
        var root = DetectSteamRoot();
        if (root is null)
        {
            return [];
        }

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        var libraryFolders = System.IO.Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFolders))
        {
            foreach (Match match in PathRegex.Matches(File.ReadAllText(libraryFolders)))
            {
                var folder = match.Groups[1].Value.Replace(@"\\", @"\").Replace('/', '\\');
                if (Directory.Exists(folder))
                {
                    libraries.Add(folder);
                }
            }
        }

        var games = new List<LibraryGame>();
        foreach (var library in libraries)
        {
            var steamApps = System.IO.Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            foreach (var manifest in Directory.GetFiles(steamApps, "appmanifest_*.acf"))
            {
                try
                {
                    var content = File.ReadAllText(manifest);
                    var appId = AppIdRegex.Match(content);
                    var name = NameRegex.Match(content);
                    var installDir = InstallDirRegex.Match(content);
                    if (!appId.Success || !name.Success || !installDir.Success)
                    {
                        continue;
                    }

                    var installPath = System.IO.Path.Combine(steamApps, "common", installDir.Groups[1].Value);
                    var process = GuessProcessName(installPath);
                    games.Add(new LibraryGame
                    {
                        Name = name.Groups[1].Value,
                        Platform = GamePlatform.Steam,
                        AppId = appId.Groups[1].Value,
                        InstallPath = Directory.Exists(installPath) ? installPath : null,
                        LaunchFile = process is null ? null : process + ".exe",
                        ProcessName = process ?? string.Empty
                    });
                }
                catch
                {
                    // Skip bad manifests.
                }
            }
        }

        return games
            .GroupBy(game => game.AppId ?? game.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GuessProcessName(string installPath)
    {
        if (!Directory.Exists(installPath))
        {
            return null;
        }

        try
        {
            var exe = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .FirstOrDefault(name =>
                    !string.IsNullOrWhiteSpace(name)
                    && !name.Contains("UnityCrash", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("CrashReport", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("installscript", StringComparison.OrdinalIgnoreCase));

            if (exe is not null)
            {
                return exe;
            }

            return Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories)
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        }
        catch
        {
            return null;
        }
    }
}
