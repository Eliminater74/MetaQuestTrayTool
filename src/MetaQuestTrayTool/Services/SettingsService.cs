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
    private readonly object _saveLock = new();

    public AppSettings Current { get; private set; } = new();

    /// <summary>True when settings.json could not be used as-is (defaults or a backup were used).</summary>
    public bool UsedFallbackSettings { get; private set; }

    /// <summary>True when the loaded settings came from settings.json.bak or .bak2.</summary>
    public bool RestoredFromBackup { get; private set; }

    public void Load()
    {
        lock (_saveLock)
        {
            AppPaths.EnsureAppDataDirectory();
            List<GameProfile>? legacyProfiles = null;
            UsedFallbackSettings = false;
            RestoredFromBackup = false;

            var primary = AppPaths.SettingsFile;
            var bak = primary + ".bak";
            var bak2 = primary + ".bak2";

            var primaryOk = TryLoadSettingsFile(primary, out var primarySettings, out var primaryLen, ref legacyProfiles);
            var bakOk = TryLoadSettingsFile(bak, out var bakSettings, out var bakLen, ref legacyProfiles);
            var bak2Ok = TryLoadSettingsFile(bak2, out var bak2Settings, out var bak2Len, ref legacyProfiles);

            if (primaryOk &&
                !IsTruncatedRelativeTo(primaryLen, bakLen) &&
                !IsTruncatedRelativeTo(primaryLen, bak2Len))
            {
                Current = primarySettings!;
            }
            else if (TryPickBackup(
                         primaryOk,
                         primaryLen,
                         bakOk,
                         bakSettings,
                         bakLen,
                         bak2Ok,
                         bak2Settings,
                         bak2Len,
                         out var recovered,
                         out var recoveredPath))
            {
                Current = recovered!;
                RestoredFromBackup = true;
                UsedFallbackSettings = true;
                QuarantineFile(primary);
                // Persist recovered settings so the next start uses a healthy primary file.
            }
            else if (primaryOk)
            {
                // Parsed but looked truncated and no usable backup — keep what we have.
                Current = primarySettings!;
                UsedFallbackSettings = true;
            }
            else if (File.Exists(primary))
            {
                UsedFallbackSettings = true;
                QuarantineFile(primary);
                Current = new AppSettings();
            }
            // else: first run — Current stays as new AppSettings()

            var storedProfiles = _profileStore.Load();
            if (_profileStore.RestoredFromBackup)
            {
                UsedFallbackSettings = true;
                RestoredFromBackup = true;
            }

            if (storedProfiles.Count > 0)
            {
                Current.Profiles = storedProfiles;
            }
            else if (legacyProfiles is { Count: > 0 })
            {
                Current.Profiles = legacyProfiles;
            }

            EnsureGlobalDefaults();
            // Always allow saves after load — never leave the user stuck with checkboxes that do not persist.
            SaveUnlocked();
        }
    }

    public void Save()
    {
        lock (_saveLock)
        {
            SaveUnlocked();
        }
    }

    public void ResetKeepingProfiles()
    {
        lock (_saveLock)
        {
            var profiles = Current.Profiles.ToList();
            Current = new AppSettings { Profiles = profiles };
            UsedFallbackSettings = false;
            RestoredFromBackup = false;
            EnsureGlobalDefaults();
            SaveUnlocked();
        }
    }

    public void Export(string path)
    {
        lock (_saveLock)
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
        lock (_saveLock)
        {
            Current = settings ?? throw new InvalidOperationException("The backup file could not be read.");
            UsedFallbackSettings = false;
            RestoredFromBackup = false;
            _profileStore.Save(Current.Profiles);
            SaveUnlocked();
        }
    }

    private void SaveUnlocked()
    {
        AppPaths.EnsureAppDataDirectory();
        _profileStore.Save(Current.Profiles);

        Current.LastSavedUtc = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        var path = AppPaths.SettingsFile;
        RotateBackups(path, path + ".bak", path + ".bak2");
        WriteDurable(path, json);
    }

    private static bool TryPickBackup(
        bool primaryOk,
        long primaryLen,
        bool bakOk,
        AppSettings? bakSettings,
        long bakLen,
        bool bak2Ok,
        AppSettings? bak2Settings,
        long bak2Len,
        out AppSettings? recovered,
        out string recoveredPath)
    {
        recovered = null;
        recoveredPath = "";

        var preferBackup = !primaryOk ||
                           IsTruncatedRelativeTo(primaryLen, bakLen) ||
                           IsTruncatedRelativeTo(primaryLen, bak2Len);
        if (!preferBackup)
        {
            return false;
        }

        if (bakOk && bak2Ok)
        {
            if (bakLen >= bak2Len)
            {
                recovered = bakSettings;
                recoveredPath = "settings.json.bak";
                return true;
            }

            recovered = bak2Settings;
            recoveredPath = "settings.json.bak2";
            return true;
        }

        if (bakOk)
        {
            recovered = bakSettings;
            recoveredPath = "settings.json.bak";
            return true;
        }

        if (bak2Ok)
        {
            recovered = bak2Settings;
            recoveredPath = "settings.json.bak2";
            return true;
        }

        return false;
    }

    private static bool IsTruncatedRelativeTo(long fileLen, long otherLen) =>
        otherLen >= 500 && fileLen < Math.Max(80, otherLen / 2);

    private static bool TryLoadSettingsFile(
        string path,
        out AppSettings? settings,
        out long length,
        ref List<GameProfile>? legacyProfiles)
    {
        settings = null;
        length = 0;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            length = new FileInfo(path).Length;
            if (length < 20)
            {
                return false;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() is "{" or "{}")
            {
                return false;
            }

            legacyProfiles ??= TryReadLegacyProfiles(json);
            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings is not null;
        }
        catch
        {
            return false;
        }
    }

    private static List<GameProfile>? TryReadLegacyProfiles(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Profiles", out var profilesElement) &&
                !doc.RootElement.TryGetProperty("profiles", out profilesElement))
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

    private static void RotateBackups(string path, string bak, string bak2)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var len = new FileInfo(path).Length;
            if (len < 20)
            {
                return;
            }

            if (File.Exists(bak))
            {
                var bakLen = new FileInfo(bak).Length;
                if (bakLen >= 500 && len < bakLen / 2)
                {
                    // Keep the healthier backup; do not promote a truncated primary over it.
                    return;
                }

                File.Copy(bak, bak2, overwrite: true);
            }

            File.Copy(path, bak, overwrite: true);
        }
        catch
        {
            // backup is best-effort
        }
    }

    /// <summary>Write then flush to disk so a power cut cannot leave a half-written JSON file.</summary>
    internal static void WriteDurable(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";

        using (var fs = new FileStream(
                   tmp,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 4096,
                   FileOptions.WriteThrough))
        using (var writer = new StreamWriter(fs))
        {
            writer.Write(contents);
            writer.Flush();
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Replace(tmp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    private static void QuarantineFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var dest = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(path, dest, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
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
        Current.HeadsetAnnouncer.VoiceName ??= "";
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

            if (Current.HotKeys.Bindings.All(binding => binding.Action != HotKeyAction.StartSteamVr))
            {
                Current.HotKeys.Bindings.Add(new HotKeyBinding
                {
                    Id = 0,
                    Action = HotKeyAction.StartSteamVr,
                    Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift,
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
