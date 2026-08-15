using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// User profiles live in profiles.json (not SQL — simpler backup/export, no server, perfect for a tray app).
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ProfilesFile { get; } = Path.Combine(AppPaths.AppDataDirectory, "profiles.json");

    public bool LastLoadFailed { get; private set; }

    public List<GameProfile> Load()
    {
        LastLoadFailed = false;
        AppPaths.EnsureAppDataDirectory();
        if (!File.Exists(ProfilesFile))
        {
            return [];
        }

        if (TryRead(ProfilesFile, out var profiles))
        {
            return profiles;
        }

        var backup = ProfilesFile + ".bak";
        if (File.Exists(backup) && TryRead(backup, out profiles))
        {
            return profiles;
        }

        LastLoadFailed = true;
        return [];
    }

    private static bool TryRead(string path, out List<GameProfile> profiles)
    {
        profiles = [];
        try
        {
            var json = File.ReadAllText(path);
            profiles = JsonSerializer.Deserialize<List<GameProfile>>(json, JsonOptions) ?? [];
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Save(IReadOnlyList<GameProfile> profiles)
    {
        AppPaths.EnsureAppDataDirectory();
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        var temp = ProfilesFile + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(ProfilesFile))
        {
            try
            {
                File.Copy(ProfilesFile, ProfilesFile + ".bak", overwrite: true);
            }
            catch
            {
                // backup is best-effort
            }
        }

        File.Move(temp, ProfilesFile, overwrite: true);
    }
}
