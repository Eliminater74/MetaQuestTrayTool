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

    private readonly ProfileStore _profileStore = new();

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        AppPaths.EnsureAppDataDirectory();
        List<GameProfile>? legacyProfiles = null;

        if (File.Exists(AppPaths.SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                legacyProfiles = TryReadLegacyProfiles(json);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                Current = new AppSettings();
            }
        }

        var storedProfiles = _profileStore.Load();
        if (storedProfiles.Count > 0)
        {
            Current.Profiles = storedProfiles;
        }
        else if (legacyProfiles is { Count: > 0 })
        {
            Current.Profiles = legacyProfiles;
            _profileStore.Save(Current.Profiles);
        }

        EnsureGlobalDefaults();
        Save();
    }

    public void Save()
    {
        AppPaths.EnsureAppDataDirectory();
        _profileStore.Save(Current.Profiles);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }

    public void ResetKeepingProfiles()
    {
        var profiles = Current.Profiles.ToList();
        Current = new AppSettings { Profiles = profiles };
        Save();
    }

    public void Export(string path)
    {
        var backup = new SettingsBackupFile
        {
            App = AppInfo.ProductName,
            Version = AppInfo.Version,
            ExportedAt = DateTimeOffset.Now,
            Settings = Current
        };
        var json = JsonSerializer.Serialize(backup, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void Import(string path)
    {
        var json = File.ReadAllText(path);
        AppSettings? settings = null;
        try
        {
            var backup = JsonSerializer.Deserialize<SettingsBackupFile>(json, JsonOptions);
            if (backup?.Settings is not null)
            {
                settings = backup.Settings;
            }
        }
        catch
        {
            // fall through to raw settings.json
        }

        settings ??= JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        Current = settings ?? throw new InvalidOperationException("The backup file could not be read.");
        _profileStore.Save(Current.Profiles);
        Save();
    }

    private static List<GameProfile>? TryReadLegacyProfiles(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Profiles", out var profilesElement))
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<GameProfile>>(profilesElement.GetRawText(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void EnsureGlobalDefaults()
    {
        Current.DefaultGameSettings ??= new GameSettings();
        Current.LinkSettings ??= new LinkSettings();
        Current.OpenXr ??= new OpenXrSettings();
    }
}