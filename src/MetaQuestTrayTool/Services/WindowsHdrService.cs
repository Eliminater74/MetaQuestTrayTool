using System.IO;
using System.Text.Json;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Turns advanced color off for displays that currently have it on, then restores
/// only those displays. A snapshot is written before the first change so a crash
/// can be recovered on the next start. The session helper must not call this.
/// </summary>
public sealed class WindowsHdrService
{
    public const string SnapshotAppName = "MetaQuestTrayTool";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IWindowsHdrDisplay _display;
    private readonly string _snapshotPath;
    private readonly object _gate = new();
    private List<HdrRestoreDisplay>? _session;

    public WindowsHdrService(IWindowsHdrDisplay display, string snapshotPath)
    {
        _display = display;
        _snapshotPath = snapshotPath;
    }

    public static string DefaultSnapshotPath =>
        Path.Combine(AppPaths.AppDataDirectory, "hdr-restore.json");

    public HdrActionResult DisableForSession()
    {
        lock (_gate)
        {
            try
            {
                var recovered = RecoverStaleSnapshotCore();
                var disabled = DisableEnabledDisplaysCore();
                return Combine(recovered, disabled);
            }
            catch (Exception ex)
            {
                return HdrActionResult.Warn("HDR control unsupported/failed: " + ex.Message);
            }
        }
    }

    public HdrActionResult RecoverStaleSnapshot()
    {
        lock (_gate)
        {
            try
            {
                return RecoverStaleSnapshotCore();
            }
            catch (Exception ex)
            {
                return HdrActionResult.Warn("HDR control unsupported/failed: " + ex.Message);
            }
        }
    }

    public HdrActionResult RestoreSession()
    {
        lock (_gate)
        {
            try
            {
                if (_session is not null)
                {
                    return RestoreRecorded(_session);
                }

                var loaded = LoadSnapshot(out var loadError);
                if (loadError is not null)
                {
                    return HdrActionResult.Warn("HDR control unsupported/failed: " + loadError);
                }

                return RestoreRecorded(loaded);
            }
            catch (Exception ex)
            {
                return HdrActionResult.Warn("HDR control unsupported/failed: " + ex.Message);
            }
        }
    }

    private HdrActionResult RecoverStaleSnapshotCore()
    {
        var recorded = LoadSnapshot(out var loadError);
        if (loadError is not null)
        {
            return HdrActionResult.Warn("HDR control unsupported/failed: " + loadError);
        }

        if (recorded is null || recorded.Count == 0)
        {
            return HdrActionResult.Ok(string.Empty);
        }

        return RestoreRecorded(recorded);
    }

    private HdrActionResult DisableEnabledDisplaysCore()
    {
        if (!_display.TryQueryActive(out var displays, out var error))
        {
            return HdrActionResult.Warn("HDR control unsupported/failed: " + (error ?? "display query failed"));
        }

        var enabled = displays
            .Where(display => display.AdvancedColorSupported && display.AdvancedColorEnabled)
            .ToList();
        var preserved = PreservedUnmatchedRecords(displays);
        if (enabled.Count == 0)
        {
            var capable = displays.Any(display => display.AdvancedColorSupported);
            var idleSummary = capable
                ? "HDR was already disabled; no change."
                : "No HDR-capable display is active; no change.";
            if (preserved.Count > 0)
            {
                SaveSnapshot(preserved);
                idleSummary += " Kept an HDR recovery snapshot for displays that are not currently connected.";
            }

            return HdrActionResult.Ok(idleSummary);
        }

        var pending = enabled.Select(display => new HdrRestoreDisplay
        {
            AdapterLuid = display.Id.AdapterLuid,
            TargetId = display.Id.TargetId,
            FriendlyName = display.FriendlyName,
            WasEnabled = true
        }).ToList();

        // Persist before changing anything so a crash can still find these targets.
        SaveSnapshot(preserved.Concat(pending).ToList());

        var notes = new List<string>();
        var changed = new List<HdrRestoreDisplay>();
        var failed = false;
        foreach (var record in pending)
        {
            var id = new HdrDisplayId(record.AdapterLuid, record.TargetId);
            if (_display.TrySetAdvancedColor(id, enabled: false, out var setError))
            {
                changed.Add(record);
                notes.Add("HDR temporarily disabled on " + record.FriendlyName + ".");
                continue;
            }

            failed = true;
            notes.Add("HDR control unsupported/failed: " + record.FriendlyName + ": " + (setError ?? "unknown error"));
        }

        SaveSnapshot(preserved.Concat(changed).ToList());
        var summary = string.Join(" ", notes);
        return failed ? HdrActionResult.Warn(summary) : HdrActionResult.Ok(summary);
    }

    private List<HdrRestoreDisplay> PreservedUnmatchedRecords(IReadOnlyList<HdrDisplayState> displays)
    {
        var existing = _session ?? LoadSnapshot(out _) ?? [];
        return existing
            .Where(record => record.WasEnabled)
            .Where(record => !displays.Any(display =>
                display.Id.AdapterLuid == record.AdapterLuid && display.Id.TargetId == record.TargetId))
            .ToList();
    }

    private HdrActionResult RestoreRecorded(List<HdrRestoreDisplay>? recorded)
    {
        if (recorded is null || recorded.Count == 0)
        {
            _session = null;
            return HdrActionResult.Ok(string.Empty);
        }

        var owned = recorded.Where(record => record.WasEnabled).ToList();
        if (owned.Count == 0)
        {
            DeleteSnapshot();
            return HdrActionResult.Ok(string.Empty);
        }

        if (!_display.TryQueryActive(out var displays, out var error))
        {
            return HdrActionResult.Warn("HDR control unsupported/failed: " + (error ?? "display query failed"));
        }

        var byId = displays.ToDictionary(display => display.Id);
        var matched = owned.Where(record => byId.ContainsKey(new HdrDisplayId(record.AdapterLuid, record.TargetId))).ToList();
        if (matched.Count == 0)
        {
            return HdrActionResult.Warn(
                "HDR recovery snapshot does not match the current displays; leaving HDR unchanged.");
        }

        var notes = new List<string>();
        var stillPending = new List<HdrRestoreDisplay>();
        var failed = false;
        foreach (var record in owned)
        {
            var id = new HdrDisplayId(record.AdapterLuid, record.TargetId);
            if (!byId.TryGetValue(id, out var current))
            {
                stillPending.Add(record);
                notes.Add("HDR display " + record.FriendlyName + " is no longer present; not changing another monitor.");
                continue;
            }

            if (!current.AdvancedColorSupported)
            {
                notes.Add("HDR control unsupported/failed: " + record.FriendlyName + " no longer reports advanced color.");
                continue;
            }

            if (current.AdvancedColorEnabled)
            {
                notes.Add("HDR already enabled on " + record.FriendlyName + "; no change.");
                continue;
            }

            if (_display.TrySetAdvancedColor(id, enabled: true, out var setError))
            {
                notes.Add("HDR restored on " + record.FriendlyName + ".");
                continue;
            }

            failed = true;
            stillPending.Add(record);
            notes.Add("HDR control unsupported/failed: " + record.FriendlyName + ": " + (setError ?? "unknown error"));
        }

        SaveSnapshot(stillPending);
        var summary = string.Join(" ", notes);
        return failed ? HdrActionResult.Warn(summary) : HdrActionResult.Ok(summary);
    }

    private List<HdrRestoreDisplay>? LoadSnapshot(out string? error)
    {
        error = null;
        if (!File.Exists(_snapshotPath))
        {
            return null;
        }

        HdrRestoreSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<HdrRestoreSnapshot>(File.ReadAllText(_snapshotPath), JsonOptions);
        }
        catch (Exception ex)
        {
            error = "could not read the HDR recovery snapshot (" + ex.Message + ")";
            return null;
        }

        if (snapshot is null
            || !string.Equals(snapshot.App, SnapshotAppName, StringComparison.Ordinal))
        {
            error = "HDR recovery snapshot is not from this app; leaving HDR unchanged.";
            return null;
        }

        return snapshot.Displays ?? [];
    }

    private void SaveSnapshot(List<HdrRestoreDisplay> displays)
    {
        if (displays.Count == 0)
        {
            DeleteSnapshot();
            return;
        }

        var directory = Path.GetDirectoryName(_snapshotPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = new HdrRestoreSnapshot
        {
            App = SnapshotAppName,
            Displays = displays
        };
        var temp = _snapshotPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(temp, _snapshotPath, overwrite: true);
        _session = displays;
    }

    private void DeleteSnapshot()
    {
        _session = null;
        if (File.Exists(_snapshotPath))
        {
            File.Delete(_snapshotPath);
        }
    }

    private static HdrActionResult Combine(HdrActionResult first, HdrActionResult second)
    {
        var parts = new[] { first.Summary, second.Summary }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return new HdrActionResult(first.Succeeded && second.Succeeded, string.Join(" ", parts));
    }

    private sealed class HdrRestoreSnapshot
    {
        public int Version { get; set; } = 1;
        public string App { get; set; } = SnapshotAppName;
        public List<HdrRestoreDisplay>? Displays { get; set; }
    }
}

internal sealed class HdrRestoreDisplay
{
    public long AdapterLuid { get; set; }
    public uint TargetId { get; set; }
    public string FriendlyName { get; set; } = "";
    public bool WasEnabled { get; set; }
}
