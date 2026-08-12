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

    public List<GameProfile> Load()
    {
        AppPaths.EnsureAppDataDirectory();
        if (!File.Exists(ProfilesFile))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(ProfilesFile);
            return JsonSerializer.Deserialize<List<GameProfile>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<GameProfile> profiles)
    {
        AppPaths.EnsureAppDataDirectory();
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(ProfilesFile, json);
    }
}
