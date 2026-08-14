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
            if (!TryLoadSettingsFile(AppPaths.SettingsFile, ref legacyProfiles))
            {
                var backup = AppPaths.SettingsFile + ".bak";
                if (File.Exists(backup))
                {
                    TryLoadSettingsFile(backup, ref legacyProfiles);
                }

                Current ??= new AppSettings();
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
        var path = AppPaths.SettingsFile;
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(path))
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch
            {
                // backup is best-effort
            }
        }

        File.Move(temp, path, overwrite: true);
    }

    private bool TryLoadSettingsFile(string path, ref List<GameProfile>? legacyProfiles)
    {
        try
        {
            var json = File.ReadAllText(path);
            legacyProfiles = TryReadLegacyProfiles(json);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return true;
        }
        catch
        {
            return false;
        }
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
        Current.DashToSteamVr ??= new DashToSteamVrSettings();
        Current.HotKeys ??= new HotKeySettings();
        Current.Voice ??= new VoiceSettings();
        Current.HeadsetAnnouncer ??= new HeadsetAnnouncerSettings();
        if (Current.HotKeys.Bindings.Count == 0)
        {
            Current.HotKeys.Bindings = HotKeySettings.CreateDefaultBindings();
        }
        else
        {
            if (Current.HotKeys.Bindings.All(binding => binding.Action != HotKeyAction.OpenMetaLink))
            {
                Current.HotKeys.Bindings.Add(new HotKeyBinding
                {
                    Id = 0,
                    Action = HotKeyAction.OpenMetaLink,
                    Modifiers = HotKeyModifiers.Control,
                    Key = "NumPad9"
                });
            }

            if (Current.HotKeys.Bindings.All(binding => binding.Action != HotKeyAction.DashToSteamVr))
            {
                Current.HotKeys.Bindings.Add(new HotKeyBinding
                {
                    Id = 0,
                    Action = HotKeyAction.DashToSteamVr,
                    Modifiers = HotKeyModifiers.Control,
                    Key = "NumPad0"
                });
            }

            Current.HotKeys.EnsureBindingIds();
        }

        if (Current.Tray.EnableHotKeys && !Current.HotKeys.Enabled)
        {
            Current.HotKeys.Enabled = true;
        }
        else if (Current.HotKeys.Enabled)
        {
            Current.Tray.EnableHotKeys = true;
        }
    }
}