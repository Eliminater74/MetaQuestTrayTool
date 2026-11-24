using System.IO;
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
    private AdbPropertySnapshot? _memory;

    public AdbPropertyTransactionService(IHeadsetPropertyStore store, string? recoveryPath = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _recoveryPath = string.IsNullOrWhiteSpace(recoveryPath) ? null : recoveryPath;
    }

    public PropertyTransactionResult Apply(
        string serial,
        string? model,
        string? fingerprint,
        IReadOnlyDictionary<string, string> changes,
        DateTimeOffset? capturedUtc = null,
        CancellationToken cancellation = default)
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

    public PropertyTransactionResult Restore(string serial, string? fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        lock (_gate)
        {
            var snapshot = LoadRecovery();
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

            var details = new List<string>();
            var incomplete = false;
            var conflict = false;
            foreach (var property in snapshot.Properties)
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
                if (property.OriginalPresence == PropertyPresence.AbsentOrEmpty)
                {
                    if (currentPresence == PropertyPresence.AbsentOrEmpty)
                    {
                        details.Add($"{property.Name}: already absent or empty.");
                        continue;
                    }

                    incomplete = true;
                    details.Add($"{property.Name}: original value was absent or empty. setprop cannot delete it. Reboot the headset to clear '{current}'.");
                    continue;
                }

                if (string.Equals(current, property.OriginalValue, StringComparison.Ordinal))
                {
                    details.Add($"{property.Name}: already at the original value.");
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
            }

            var status = conflict
                ? PropertyTransactionStatus.Conflict
                : incomplete ? PropertyTransactionStatus.Incomplete : PropertyTransactionStatus.Restored;
            if (status == PropertyTransactionStatus.Restored)
            {
                DeleteRecovery();
            }

            return new PropertyTransactionResult
            {
                Status = status,
                Summary = status switch
                {
                    PropertyTransactionStatus.Restored => "Original headset properties were restored and the snapshot was cleared.",
                    PropertyTransactionStatus.Conflict => "Restore left conflicting properties unchanged.",
                    _ => "Restore is incomplete. A reboot may still be required."
                },
                Details = details,
                Snapshot = status == PropertyTransactionStatus.Restored ? null : snapshot
            };
        }
    }

    public PropertyTransactionResult DiscardRecovery(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        lock (_gate)
        {
            var snapshot = LoadRecovery();
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
        if (_recoveryPath is not null && File.Exists(_recoveryPath))
        {
            try
            {
                _memory = JsonSerializer.Deserialize<AdbPropertySnapshot>(File.ReadAllText(_recoveryPath));
                return _memory;
            }
            catch (JsonException)
            {
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
