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

    public bool RestoredFromBackup { get; private set; }

    public List<GameProfile> Load()
    {
        LastLoadFailed = false;
        RestoredFromBackup = false;
        AppPaths.EnsureAppDataDirectory();

        var primary = ProfilesFile;
        var bak = primary + ".bak";
        var bak2 = primary + ".bak2";

        var primaryOk = TryRead(primary, out var primaryList, out var primaryLen);
        var bakOk = TryRead(bak, out var bakList, out var bakLen);
        var bak2Ok = TryRead(bak2, out var bak2List, out var bak2Len);

        // Empty primary with a non-empty backup is typical after a power-loss wipe of [].
        if (primaryOk && primaryList.Count > 0 &&
            !IsTruncatedRelativeTo(primaryLen, bakLen) &&
            !IsTruncatedRelativeTo(primaryLen, bak2Len))
        {
            return primaryList;
        }

        if (primaryOk && primaryList.Count == 0 && !bakOk && !bak2Ok)
        {
            return primaryList;
        }

        if (TryPickBackup(bakOk, bakList, bakLen, bak2Ok, bak2List, bak2Len, out var recovered))
        {
            RestoredFromBackup = true;
            QuarantineFile(primary);
            try
            {
                Save(recovered);
            }
            catch
            {
                // best-effort rewrite
            }

            return recovered;
        }

        if (primaryOk)
        {
            return primaryList;
        }

        if (File.Exists(primary))
        {
            LastLoadFailed = true;
            QuarantineFile(primary);
        }

        return [];
    }

    public void Save(IReadOnlyList<GameProfile> profiles)
    {
        AppPaths.EnsureAppDataDirectory();
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        var path = ProfilesFile;
        RotateBackups(path, path + ".bak", path + ".bak2");
        SettingsService.WriteDurable(path, json);
    }

    private static bool TryPickBackup(
        bool bakOk,
        List<GameProfile> bakList,
        long bakLen,
        bool bak2Ok,
        List<GameProfile> bak2List,
        long bak2Len,
        out List<GameProfile> recovered)
    {
        recovered = [];
        if (bakOk && bakList.Count > 0 && bak2Ok && bak2List.Count > 0)
        {
            if (bakLen >= bak2Len)
            {
                recovered = bakList;
                return true;
            }

            recovered = bak2List;
            return true;
        }

        if (bakOk && bakList.Count > 0)
        {
            recovered = bakList;
            return true;
        }

        if (bak2Ok && bak2List.Count > 0)
        {
            recovered = bak2List;
            return true;
        }

        return false;
    }

    private static bool IsTruncatedRelativeTo(long fileLen, long otherLen) =>
        otherLen >= 50 && fileLen < Math.Max(4, otherLen / 2);

    private static bool TryRead(string path, out List<GameProfile> profiles, out long length)
    {
        profiles = [];
        length = 0;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            length = new FileInfo(path).Length;
            if (length < 2)
            {
                return false;
            }

            var json = File.ReadAllText(path);
            profiles = JsonSerializer.Deserialize<List<GameProfile>>(json, JsonOptions) ?? [];
            return true;
        }
        catch
        {
            return false;
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
            if (len < 2)
            {
                return;
            }

            // Do not promote an empty [] over a backup that still has profiles.
            if (File.Exists(bak))
            {
                var bakLen = new FileInfo(bak).Length;
                if (bakLen >= 50 && len < bakLen / 2)
                {
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
}
