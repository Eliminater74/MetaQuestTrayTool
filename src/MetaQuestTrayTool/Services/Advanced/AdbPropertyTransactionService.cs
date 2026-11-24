using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public interface IHeadsetPropertyStore
{
    string? Read(string serial, string name);
    void Write(string serial, string name, string value);
}

public enum PropertyPresence
{
    AbsentOrEmpty,
    Present
}

public enum PropertyTransactionStatus
{
    Verified,
    Restored,
    Incomplete,
    Conflict,
    Failed,
    RolledBack
}

public sealed class TrackedHeadsetProperty
{
    public string Name { get; init; } = string.Empty;
    public PropertyPresence OriginalPresence { get; init; }
    public string? OriginalValue { get; init; }
    public string? AppliedValue { get; init; }
}

public sealed class AdbPropertySnapshot
{
    public string Serial { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string? Fingerprint { get; init; }
    public string? BootId { get; init; }
    public string Owner { get; init; } = AdbPropertyTransactionService.OwnerId;
    public DateTimeOffset CapturedUtc { get; init; }
    public List<TrackedHeadsetProperty> Properties { get; init; } = [];
}

public sealed class PropertyTransactionResult
{
    public PropertyTransactionStatus Status { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Details { get; init; } = [];
    public AdbPropertySnapshot? Snapshot { get; init; }

    public string ToDisplayText() => Summary + (Details.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, Details));
}

public sealed class AdbPropertyTransactionService
{
    public const string OwnerId = "metaquesttraytool";
    private static readonly Regex NamePattern = new(@"^[A-Za-z][A-Za-z0-9._]{0,127}$", RegexOptions.Compiled);
    private static readonly Regex ValuePattern = new(@"^[A-Za-z0-9._:+-]{1,64}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IHeadsetPropertyStore _store;
    private readonly string? _recoveryPath;
    private readonly object _gate = new();
    private readonly string _operationKey;
    private AdbPropertySnapshot? _memory;
    private string? _corruptDetail;

    public AdbPropertyTransactionService(IHeadsetPropertyStore store, string? recoveryPath = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _recoveryPath = string.IsNullOrWhiteSpace(recoveryPath) ? null : recoveryPath;
        _operationKey = _recoveryPath ?? "memory:" + RuntimeHelpers.GetHashCode(this).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool TryBeginOperation(out int generation) => PropertyOperationCoordinator.TryEnter(_operationKey, out generation);

    public void EndOperation(int generation) => PropertyOperationCoordinator.Exit(_operationKey, generation);

    public PropertyTransactionResult Apply(
        string serial,
        string? model,
        string? fingerprint,
        IReadOnlyDictionary<string, string> changes,
        DateTimeOffset? capturedUtc = null,
        CancellationToken cancellation = default,
        string? bootId = null)
    {
        if (!TryBeginOperation(out var operation))
        {
            return Fail("Another headset property operation is in progress. Nothing was written.");
        }

        try
        {
            return ApplyCore(serial, model, fingerprint, changes, capturedUtc, cancellation, bootId);
        }
        finally
        {
            EndOperation(operation);
        }
    }

    public PropertyTransactionResult ApplyIfCurrent(
        int generation,
        string serial,
        string? model,
        string? fingerprint,
        IReadOnlyDictionary<string, string> changes,
        DateTimeOffset? capturedUtc = null,
        CancellationToken cancellation = default,
        string? bootId = null)
    {
        if (!PropertyOperationCoordinator.IsCurrent(_operationKey, generation))
        {
            return Fail("The property operation is stale. Nothing was written.");
        }

        return ApplyCore(serial, model, fingerprint, changes, capturedUtc, cancellation, bootId);
    }

    private PropertyTransactionResult ApplyCore(
        string serial,
        string? model,
        string? fingerprint,
        IReadOnlyDictionary<string, string> changes,
        DateTimeOffset? capturedUtc = null,
        CancellationToken cancellation = default,
        string? bootId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (changes is null || changes.Count == 0)
        {
            return Fail("No headset properties were requested.");
        }

        var validation = Validate(changes);
        if (validation is not null)
        {
            return validation;
        }

        lock (_gate)
        {
            var existing = LoadRecovery();
            if (_corruptDetail is not null)
            {
                return Fail("The property recovery file is corrupt (" + _corruptDetail + "). Nothing was written. The file was left in place.");
            }

            if (existing is not null && !string.Equals(existing.Owner, OwnerId, StringComparison.Ordinal))
            {
                return Fail("The stored snapshot has a different owner. Nothing was written.");
            }

            if (existing is not null && !string.Equals(existing.Serial, serial, StringComparison.Ordinal))
            {
                return Fail("A property snapshot for a different headset is still stored. Restore or discard that snapshot before applying overrides to this headset.");
            }

            if (existing is not null
                && !string.IsNullOrWhiteSpace(existing.Fingerprint)
                && !string.IsNullOrWhiteSpace(fingerprint)
                && !string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Fail("The stored snapshot belongs to a different firmware fingerprint. Restore or discard it before applying new overrides. Nothing was written.");
            }

            if (existing is not null
                && !string.IsNullOrWhiteSpace(existing.BootId)
                && !string.IsNullOrWhiteSpace(bootId)
                && !string.Equals(existing.BootId, bootId, StringComparison.Ordinal))
            {
                return Fail("The headset boot id changed since the snapshot. Restore or discard that snapshot before applying. Nothing was written.");
            }

            if (cancellation.IsCancellationRequested)
            {
                return Fail("Cancelled before any property was written.");
            }

            var tracked = new Dictionary<string, TrackedHeadsetProperty>(StringComparer.Ordinal);
            if (existing is not null)
            {
                foreach (var property in existing.Properties)
                {
                    tracked[property.Name] = property;
                }
            }

            foreach (var (name, value) in changes)
            {
                if (tracked.TryGetValue(name, out var previous))
                {
                    tracked[name] = new TrackedHeadsetProperty
                    {
                        Name = previous.Name,
                        OriginalPresence = previous.OriginalPresence,
                        OriginalValue = previous.OriginalValue,
                        AppliedValue = value
                    };
                    continue;
                }

                var original = ReadQuiet(serial, name, out var readError);
                if (readError is not null)
                {
                    return Fail(readError);
                }

                tracked[name] = new TrackedHeadsetProperty
                {
                    Name = name,
                    OriginalPresence = string.IsNullOrWhiteSpace(original) ? PropertyPresence.AbsentOrEmpty : PropertyPresence.Present,
                    OriginalValue = string.IsNullOrWhiteSpace(original) ? null : original,
                    AppliedValue = value
                };
            }

            var snapshot = new AdbPropertySnapshot
            {
                Serial = serial,
                Model = string.IsNullOrWhiteSpace(model) ? existing?.Model : model,
                Fingerprint = string.IsNullOrWhiteSpace(fingerprint) ? existing?.Fingerprint : fingerprint,
                Owner = OwnerId,
                BootId = string.IsNullOrWhiteSpace(bootId) ? existing?.BootId : bootId,
                CapturedUtc = existing?.CapturedUtc ?? capturedUtc ?? DateTimeOffset.UtcNow,
                Properties = tracked.Values.ToList()
            };
            var saveError = SaveRecovery(snapshot);
            if (saveError is not null)
            {
                return Fail(saveError);
            }

            var details = new List<string>();
            var written = new List<TrackedHeadsetProperty>();
            foreach (var (name, _) in changes)
            {
                if (cancellation.IsCancellationRequested)
                {
                    if (written.Count == 0)
                    {
                        return Fail("Cancelled before any property was written.");
                    }

                    details.Add("Cancelled.");
                    return Rollback(serial, written, details, "Apply was cancelled. Completed writes were rolled back.");
                }

                var property = tracked[name];
                try
                {
                    _store.Write(serial, property.Name, property.AppliedValue!);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
                {
                    details.Add($"{property.Name}: write failed ({ex.GetType().Name}).");
                    return Rollback(serial, written, details, "Apply stopped because a write failed.");
                }

                var readback = ReadQuiet(serial, property.Name, out var readbackError);
                if (readbackError is not null || !string.Equals(readback, property.AppliedValue, StringComparison.Ordinal))
                {
                    details.Add($"{property.Name}: readback was '{readback ?? "<empty>"}', requested '{property.AppliedValue}'.");
                    written.Add(property);
                    return Rollback(serial, written, details, "Apply stopped because readback did not match the requested value.");
                }

                written.Add(property);
                details.Add($"{property.Name}: readback matched {property.AppliedValue}.");
            }

            return new PropertyTransactionResult
            {
                Status = PropertyTransactionStatus.Verified,
                Summary = $"Verified {written.Count} headset property write(s) on the selected headset.",
                Details = details,
                Snapshot = snapshot
            };
        }
    }

    public PropertyTransactionResult Restore(string serial, string? fingerprint) =>
        Restore(serial, fingerprint, onlyNames: null);

    /// <summary>
    /// Restores a subset of the snapshot and leaves every other tracked property untouched.
    /// A thermal trip uses this so an unrelated refresh override is not rolled back with the GPU property.
    /// </summary>
    public PropertyTransactionResult Restore(string serial, string? fingerprint, IReadOnlyCollection<string>? onlyNames, string? bootId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (!TryBeginOperation(out var operation))
        {
            return Fail("Another headset property operation is in progress. Nothing was written.");
        }

        try
        {
        lock (_gate)
        {
            var snapshot = LoadRecovery();
            if (_corruptDetail is not null)
            {
                return Fail("The property recovery file is corrupt (" + _corruptDetail + "). Nothing was written. The file was left in place.");
            }

            if (snapshot is null)
            {
                return Fail("No property snapshot is stored.");
            }

            if (!string.Equals(snapshot.Owner, OwnerId, StringComparison.Ordinal)
                || !string.Equals(snapshot.Serial, serial, StringComparison.Ordinal))
            {
                return Fail("The stored snapshot belongs to a different headset or owner. It was not applied.");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Fingerprint)
                && !string.IsNullOrWhiteSpace(fingerprint)
                && !string.Equals(snapshot.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Fail("The headset firmware fingerprint changed since the snapshot. Restore was refused.");
            }

            var rebooted = !string.IsNullOrWhiteSpace(snapshot.BootId)
                && !string.IsNullOrWhiteSpace(bootId)
                && !string.Equals(snapshot.BootId, bootId, StringComparison.Ordinal);

            var selected = onlyNames is null
                ? snapshot.Properties
                : snapshot.Properties.Where(property => onlyNames.Contains(property.Name, StringComparer.Ordinal)).ToList();
            if (onlyNames is not null && selected.Count == 0)
            {
                return Fail("None of the requested properties are in the stored snapshot. Nothing was written.");
            }

            var details = new List<string>();
            var incomplete = false;
            var conflict = false;
            var restoredNames = new List<string>();
            foreach (var property in selected)
            {
                var current = ReadQuiet(serial, property.Name, out var readError);
                if (readError is not null)
                {
                    details.Add($"{property.Name}: {readError}");
                    return new PropertyTransactionResult
                    {
                        Status = PropertyTransactionStatus.Failed,
                        Summary = "Restore stopped because the headset could not be read.",
                        Details = details,
                        Snapshot = snapshot
                    };
                }

                var currentPresence = string.IsNullOrWhiteSpace(current) ? PropertyPresence.AbsentOrEmpty : PropertyPresence.Present;
                if (rebooted && currentPresence == PropertyPresence.AbsentOrEmpty)
                {
                    details.Add($"{property.Name}: absent after a headset reboot. The override did not survive, so nothing was written.");
                    restoredNames.Add(property.Name);
                    continue;
                }

                if (rebooted
                    && !string.Equals(current, property.OriginalValue, StringComparison.Ordinal)
                    && !string.Equals(current, property.AppliedValue, StringComparison.Ordinal))
                {
                    conflict = true;
                    details.Add($"{property.Name}: current '{current}' changed across a reboot and matches neither the original nor the applied value. Left unchanged.");
                    continue;
                }

                if (property.OriginalPresence == PropertyPresence.AbsentOrEmpty)
                {
                    if (currentPresence == PropertyPresence.AbsentOrEmpty)
                    {
                        details.Add($"{property.Name}: already absent or empty.");
                        restoredNames.Add(property.Name);
                        continue;
                    }

                    incomplete = true;
                    details.Add($"{property.Name}: original value was absent or empty. setprop cannot delete it. Reboot the headset to clear '{current}'.");
                    continue;
                }

                if (string.Equals(current, property.OriginalValue, StringComparison.Ordinal))
                {
                    details.Add($"{property.Name}: already at the original value.");
                    restoredNames.Add(property.Name);
                    continue;
                }

                var expected = property.AppliedValue;
                if (!string.IsNullOrWhiteSpace(expected)
                    && !string.Equals(current, expected, StringComparison.Ordinal))
                {
                    conflict = true;
                    details.Add($"{property.Name}: current '{current}' matches neither the original '{property.OriginalValue}' nor the applied '{expected}'. Left unchanged.");
                    continue;
                }

                try
                {
                    _store.Write(serial, property.Name, property.OriginalValue!);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
                {
                    details.Add($"{property.Name}: restore write failed ({ex.GetType().Name}).");
                    incomplete = true;
                    continue;
                }

                var readback = ReadQuiet(serial, property.Name, out var readbackError);
                if (readbackError is not null || !string.Equals(readback, property.OriginalValue, StringComparison.Ordinal))
                {
                    incomplete = true;
                    details.Add($"{property.Name}: restore readback was '{readback ?? "<empty>"}'.");
                    continue;
                }

                details.Add($"{property.Name}: restored {property.OriginalValue}.");
                restoredNames.Add(property.Name);
            }

            var status = conflict
                ? PropertyTransactionStatus.Conflict
                : incomplete ? PropertyTransactionStatus.Incomplete : PropertyTransactionStatus.Restored;
            if (onlyNames is null)
            {
                if (status == PropertyTransactionStatus.Restored)
                {
                    DeleteRecovery();
                }
            }
            else
            {
                var remaining = snapshot.Properties.Where(property => !restoredNames.Contains(property.Name, StringComparer.Ordinal)).ToList();
                if (remaining.Count == 0)
                {
                    DeleteRecovery();
                    snapshot = null;
                }
                else
                {
                    var updated = new AdbPropertySnapshot
                    {
                        Serial = snapshot.Serial,
                        Model = snapshot.Model,
                        Fingerprint = snapshot.Fingerprint,
                        Owner = snapshot.Owner,
                        BootId = snapshot.BootId,
                        CapturedUtc = snapshot.CapturedUtc,
                        Properties = remaining
                    };
                    var saveError = SaveRecovery(updated);
                    if (saveError is not null)
                    {
                        details.Add(saveError);
                        status = PropertyTransactionStatus.Incomplete;
                        snapshot = LoadRecovery();
                    }
                    else
                    {
                        snapshot = updated;
                    }
                }
            }

            var fullyCleared = status == PropertyTransactionStatus.Restored && (onlyNames is null || snapshot is null);
            return new PropertyTransactionResult
            {
                Status = status,
                Summary = fullyCleared
                    ? "Original headset properties were restored and the snapshot was cleared."
                    : status switch
                    {
                        PropertyTransactionStatus.Restored => "Requested properties were restored. The snapshot still tracks other properties.",
                        PropertyTransactionStatus.Conflict => "Restore left conflicting properties unchanged.",
                        _ => "Restore is incomplete. A reboot may still be required."
                    },
                Details = details,
                Snapshot = fullyCleared ? null : snapshot
            };
        }
        }
        finally
        {
            EndOperation(operation);
        }
    }

    public PropertyTransactionResult DiscardRecovery(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (!TryBeginOperation(out var operation))
        {
            return Fail("Another headset property operation is in progress. Nothing was written.");
        }

        try
        {
        lock (_gate)
        {
            var snapshot = LoadRecovery();
            if (_corruptDetail is not null)
            {
                return Fail("The property recovery file is corrupt (" + _corruptDetail + "). It was not discarded.");
            }

            if (snapshot is null)
            {
                return Fail("No property snapshot is stored.");
            }

            if (!string.Equals(snapshot.Serial, serial, StringComparison.Ordinal))
            {
                return Fail("Refused to discard a snapshot that belongs to a different headset.");
            }

            DeleteRecovery();
            return new PropertyTransactionResult
            {
                Status = PropertyTransactionStatus.Restored,
                Summary = "The snapshot for this headset was discarded without writing properties."
            };
        }
        }
        finally
        {
            EndOperation(operation);
        }
    }

    public PropertyTransactionResult QuarantineCorruptRecovery()
    {
        if (_recoveryPath is null || !File.Exists(_recoveryPath))
        {
            return Fail("No recovery file is present.");
        }

        if (!TryBeginOperation(out var operation))
        {
            return Fail("Another headset property operation is in progress. Nothing was written.");
        }

        try
        {
            lock (_gate)
            {
                LoadRecovery();
                if (_corruptDetail is null)
                {
                    return Fail("The recovery file is readable. It was not moved.");
                }

                var destination = _recoveryPath + ".corrupt";
                if (File.Exists(destination))
                {
                    return Fail("A quarantined copy already exists. The corrupt file was left in place. Nothing was written.");
                }

                File.Move(_recoveryPath, destination);
                _corruptDetail = null;
                _memory = null;
                return new PropertyTransactionResult
                {
                    Status = PropertyTransactionStatus.Restored,
                    Summary = "The unreadable recovery file was moved aside. No headset properties were written."
                };
            }
        }
        finally
        {
            EndOperation(operation);
        }
    }

    public static PropertyTransactionResult? Validate(IReadOnlyDictionary<string, string> changes)
    {
        foreach (var (name, value) in changes)
        {
            if (!NamePattern.IsMatch(name)
                || !(name.StartsWith("debug.oculus.", StringComparison.Ordinal)
                    || name.StartsWith("debug.q3pw.", StringComparison.Ordinal)))
            {
                return Fail($"Rejected property name '{name}'.");
            }

            if (value is null || !ValuePattern.IsMatch(value))
            {
                return Fail($"Rejected value for {name}.");
            }
        }

        return null;
    }

    private PropertyTransactionResult Rollback(
        string serial,
        IReadOnlyList<TrackedHeadsetProperty> written,
        List<string> details,
        string summary)
    {
        var incomplete = false;
        foreach (var property in written)
        {
            if (property.OriginalPresence == PropertyPresence.AbsentOrEmpty)
            {
                incomplete = true;
                details.Add($"{property.Name}: cannot delete the original absent value. Reboot to clear.");
                continue;
            }

            try
            {
                _store.Write(serial, property.Name, property.OriginalValue!);
                var readback = ReadQuiet(serial, property.Name, out var error);
                if (error is not null || !string.Equals(readback, property.OriginalValue, StringComparison.Ordinal))
                {
                    incomplete = true;
                    details.Add($"{property.Name}: rollback readback failed.");
                }
                else
                {
                    details.Add($"{property.Name}: rolled back to {property.OriginalValue}.");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
                incomplete = true;
                details.Add($"{property.Name}: rollback failed ({ex.GetType().Name}).");
            }
        }

        return new PropertyTransactionResult
        {
            Status = incomplete ? PropertyTransactionStatus.Incomplete : PropertyTransactionStatus.RolledBack,
            Summary = summary,
            Details = details,
            Snapshot = LoadRecovery()
        };
    }

    private string? ReadQuiet(string serial, string name, out string? error)
    {
        try
        {
            error = null;
            return _store.Read(serial, name);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            error = $"Could not read {name} ({ex.GetType().Name}).";
            return null;
        }
    }

    private string? SaveRecovery(AdbPropertySnapshot snapshot)
    {
        var previous = _memory;
        _memory = snapshot;
        if (_recoveryPath is null)
        {
            return null;
        }

        try
        {
            var directory = Path.GetDirectoryName(_recoveryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _recoveryPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions));
            if (File.Exists(_recoveryPath))
            {
                File.Replace(temp, _recoveryPath, _recoveryPath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, _recoveryPath);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _memory = previous;
            return "Could not persist the property snapshot. No properties were written.";
        }
    }

    private AdbPropertySnapshot? LoadRecovery()
    {
        _corruptDetail = null;
        if (_recoveryPath is not null && File.Exists(_recoveryPath))
        {
            string text;
            try
            {
                text = File.ReadAllText(_recoveryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _corruptDetail = ex.GetType().Name;
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<AdbPropertySnapshot>(text);
                if (parsed is null || string.IsNullOrWhiteSpace(parsed.Serial) || parsed.Properties is null)
                {
                    _corruptDetail = "missing snapshot";
                    return null;
                }

                _memory = parsed;
                return _memory;
            }
            catch (JsonException)
            {
                _corruptDetail = "invalid JSON";
                return null;
            }
        }

        return _memory;
    }

    private void DeleteRecovery()
    {
        _memory = null;
        if (_recoveryPath is not null && File.Exists(_recoveryPath))
        {
            File.Delete(_recoveryPath);
        }
    }

    private static PropertyTransactionResult Fail(string summary) => new()
    {
        Status = PropertyTransactionStatus.Failed,
        Summary = summary
    };
}

internal static class PropertyOperationCoordinator
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Slot> Slots = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryEnter(string key, out int generation)
    {
        lock (Sync)
        {
            if (!Slots.TryGetValue(key, out var slot))
            {
                slot = new Slot();
                Slots[key] = slot;
            }

            if (slot.Held != 0)
            {
                generation = 0;
                return false;
            }

            slot.Held = 1;
            generation = ++slot.Generation;
            return true;
        }
    }

    public static void Exit(string key, int generation)
    {
        lock (Sync)
        {
            if (Slots.TryGetValue(key, out var slot) && slot.Held == 1 && slot.Generation == generation)
            {
                slot.Held = 0;
            }
        }
    }

    public static bool IsCurrent(string key, int generation)
    {
        lock (Sync)
        {
            return Slots.TryGetValue(key, out var slot) && slot.Held == 1 && slot.Generation == generation;
        }
    }

    private sealed class Slot
    {
        public int Held;
        public int Generation;
    }
}
