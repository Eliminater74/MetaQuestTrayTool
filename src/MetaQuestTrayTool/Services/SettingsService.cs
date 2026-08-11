using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        AppPaths.EnsureAppDataDirectory();

        if (!File.Exists(AppPaths.SettingsFile))
        {
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.SettingsFile);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        AppPaths.EnsureAppDataDirectory();
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }
}
