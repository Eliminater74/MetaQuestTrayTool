using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public enum ThermalGuardAction
{
    Continue,
    Restore
}

public sealed class ThermalReading
{
    public bool HeadsetReachable { get; init; }
    public double? BatteryCelsius { get; init; }
    public string? ThermalStatusText { get; init; }
    public DateTimeOffset SampledUtc { get; init; }
    public string? ReadError { get; init; }

    public static ThermalReading Unreachable(DateTimeOffset sampledUtc, string? error) => new()
    {
        HeadsetReachable = false,
        SampledUtc = sampledUtc,
        ReadError = error
    };
}

public sealed class ThermalGuardDecision
{
    public ThermalGuardAction Action { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Session-scoped battery and thermal watch for an experimental GPU level 7 override.
/// The 45 C figure is the same application gate used before the write. It is not a GPU junction limit,
/// not a Meta firmware threshold, and not a claim that temperatures below it are safe.
/// </summary>
public static class ExperimentalGpuThermalPolicy
{
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaxSampleAge = TimeSpan.FromSeconds(45);
    public const int MaxSamplesPerSession = 1500;

    public const string GateDisclaimer =
        "The 45 C value is an application gate on battery temperature. It is not GPU junction temperature, not a firmware thermal limit, and not an established universal hardware safety limit. Firmware thermal management is unchanged.";

    private static readonly Regex SevereStatus = new(
        @"\b(severe|critical|emergency|shutdown)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ThermalGuardDecision Evaluate(ThermalReading? reading, DateTimeOffset nowUtc)
    {
        if (reading is null || !reading.HeadsetReachable)
        {
            return Restore("The headset could not be read during an active experimental GPU override. " + (reading?.ReadError ?? "No sample.") + " " + GateDisclaimer);
        }

        if (nowUtc - reading.SampledUtc > MaxSampleAge)
        {
            return Restore("The battery reading is stale. A stale reading is not treated as safe. " + GateDisclaimer);
        }

        if (reading.BatteryCelsius is not double temperature)
        {
            return Restore("Battery temperature was unavailable during an active experimental GPU override. A missing reading is not treated as safe. " + GateDisclaimer);
        }

        if (temperature >= ExperimentalGpuPolicy.RefuseAtOrAboveCelsius)
        {
            return Restore(
                $"Battery temperature is {temperature.ToString("0.0", CultureInfo.InvariantCulture)} C, which is at or above the {ExperimentalGpuPolicy.RefuseAtOrAboveCelsius.ToString("0", CultureInfo.InvariantCulture)} C application gate. {GateDisclaimer}");
        }

        if (!string.IsNullOrWhiteSpace(reading.ThermalStatusText) && SevereStatus.IsMatch(reading.ThermalStatusText))
        {
            return Restore("The thermal status text reported a severe or higher condition. " + GateDisclaimer);
        }

        return new ThermalGuardDecision
        {
            Action = ThermalGuardAction.Continue,
            Reason = $"Battery temperature {temperature.ToString("0.0", CultureInfo.InvariantCulture)} C is under the application gate. {GateDisclaimer}"
        };
    }

    private static ThermalGuardDecision Restore(string reason) => new()
    {
        Action = ThermalGuardAction.Restore,
        Reason = reason
    };
}

public sealed class ThermalSessionOutcome
{
    public bool Watching { get; init; }
    public bool Restored { get; init; }
    public bool Cancelled { get; init; }
    public bool BlockedAutomaticReapply { get; init; }
    public bool RestorationPending { get; init; }
    public PropertyTransactionStatus? RestorationStatus { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public interface IExperimentalGpuSession
{
    bool AutomaticReapplyBlocked { get; }
    bool RestorationPending { get; }
    string? PendingSummary { get; }
    void PrepareExplicitApply();
    void BeginExplicit(string serial, string? model, string? fingerprint);
    void ExplicitApplyFailed();
    void StopWithoutRestore(string reason);
    void NoteRestoreResult(string serial, PropertyTransactionResult result);
    ThermalSessionOutcome RestoreOwnedAndStop(string reason, TimeSpan timeout);
}

public sealed class ExperimentalGpuThermalGuard : IExperimentalGpuSession, IDisposable
{
    private readonly Func<string, ThermalReading> _read;
    private readonly Func<string, string?, PropertyTransactionResult> _restoreGpu;
    private readonly Action<string>? _log;
    private readonly string? _latchPath;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancel;
    private Task? _task;
    private int _generation;
    private bool _blocked;
    private bool _pending;
    private bool _shutdown;
    private bool _suspendRestore;
    private string? _serial;
    private string? _fingerprint;
    private string? _pendingSummary;

    public ExperimentalGpuThermalGuard(
        Func<string, ThermalReading> read,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        Action<string>? log = null,
        string? latchPath = null)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _restoreGpu = restoreGpu ?? throw new ArgumentNullException(nameof(restoreGpu));
        _log = log;
        _latchPath = string.IsNullOrWhiteSpace(latchPath) ? null : latchPath;
        _blocked = LoadLatch();
    }

    public bool AutomaticReapplyBlocked
    {
        get { lock (_gate) return _blocked; }
    }

    public bool RestorationPending
    {
        get { lock (_gate) return _pending; }
    }

    public string? PendingSummary
    {
        get { lock (_gate) return _pendingSummary; }
    }

    public bool IsWatching
    {
        get { lock (_gate) return _cancel is not null; }
    }

    public string? ActiveSerial
    {
        get { lock (_gate) return _serial; }
    }

    public ThermalSessionOutcome? LastOutcome { get; private set; }

    public void AcknowledgeExplicitApply()
    {
        lock (_gate)
        {
            _blocked = false;
            _pending = false;
            _pendingSummary = null;
            _suspendRestore = false;
            DeleteLatch();
        }
    }

    public void PrepareExplicitApply()
    {
        lock (_gate)
        {
            _generation++;
            _suspendRestore = true;
            _cancel?.Cancel();
        }
    }

    public void ExplicitApplyFailed()
    {
        string? serial;
        string? fingerprint;
        var resume = false;
        lock (_gate)
        {
            _suspendRestore = false;
            serial = _serial;
            fingerprint = _fingerprint;
            resume = _pending || LeaseSaysOwned();
        }

        if (resume && !string.IsNullOrWhiteSpace(serial))
        {
            EnsureRecoveryLoop(serial, fingerprint, "The explicit GPU apply did not verify. Restoration of the previous override remains pending.");
        }
    }

    public void BeginExplicit(string serial, string? model, string? fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        CancellationToken token;
        CancellationTokenSource started;
        int generation;
        lock (_gate)
        {
            _blocked = false;
            _pending = false;
            _pendingSummary = null;
            _suspendRestore = false;
            _shutdown = false;
            _serial = serial;
            _fingerprint = fingerprint;
            _generation++;
            generation = _generation;
            _cancel?.Cancel();
            _cancel = new CancellationTokenSource();
            started = _cancel;
            token = started.Token;
            SaveOwnedLease(serial, fingerprint);
        }

        _log?.Invoke("Experimental GPU thermal watch started for " + serial + ". " + ExperimentalGpuThermalPolicy.GateDisclaimer);
        _ = model;
        var task = Task.Run(() => Watch(generation, serial, fingerprint, started, token));
        lock (_gate)
        {
            _task = task;
        }
    }

    public void StopWithoutRestore(string reason)
    {
        lock (_gate)
        {
            _generation++;
            _suspendRestore = true;
            _cancel?.Cancel();
        }

        _log?.Invoke(string.IsNullOrWhiteSpace(reason)
            ? "Experimental GPU thermal watch stopped without a thermal restoration."
            : reason);
    }

    public void NoteRestoreResult(string serial, PropertyTransactionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status == PropertyTransactionStatus.Restored)
        {
            lock (_gate)
            {
                _blocked = false;
                _pending = false;
                _pendingSummary = null;
                _suspendRestore = false;
                _serial = null;
                _fingerprint = null;
                DeleteLatch();
            }

            LastOutcome = new ThermalSessionOutcome
            {
                Restored = true,
                RestorationStatus = result.Status,
                Summary = result.Summary
            };
            return;
        }

        if (result.Status == PropertyTransactionStatus.Conflict)
        {
            lock (_gate)
            {
                _blocked = true;
                _pending = true;
                _suspendRestore = false;
                _pendingSummary = "Experimental GPU restore left an externally changed value in place. Automatic reapplication stays blocked. " + result.Summary;
                SaveLatch(serial, _pendingSummary, result.Status, failed: true, owned: false);
            }

            LastOutcome = new ThermalSessionOutcome
            {
                BlockedAutomaticReapply = true,
                RestorationPending = true,
                RestorationStatus = result.Status,
                Summary = _pendingSummary ?? result.Summary
            };
            return;
        }

        var retry = IsRetryable(result);
        lock (_gate)
        {
            _blocked = true;
            _pending = true;
            _suspendRestore = false;
            _serial = serial;
            _pendingSummary = "Experimental GPU restoration is pending (" + result.Status + "). " + result.Summary;
            SaveLatch(serial, _pendingSummary, result.Status, failed: true, owned: retry);
        }

        if (retry)
        {
            EnsureRecoveryLoop(serial, _fingerprint, _pendingSummary);
        }
    }

    private static bool IsRetryable(PropertyTransactionResult result)
    {
        if (result.Status == PropertyTransactionStatus.Incomplete)
        {
            return true;
        }

        if (result.Status != PropertyTransactionStatus.Failed)
        {
            return false;
        }

        var summary = result.Summary ?? string.Empty;
        return summary.Contains("could not be read", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("in progress", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("Thermal restoration failed", StringComparison.OrdinalIgnoreCase);
    }

    public ThermalSessionOutcome RestoreOwnedAndStop(string reason, TimeSpan timeout)
    {
        Task? task;
        lock (_gate)
        {
            _shutdown = true;
            _suspendRestore = false;
            task = _task;
            _cancel?.Cancel();
        }

        if (task is not null)
        {
            try
            {
                task.Wait(timeout);
            }
            catch (AggregateException)
            {
            }
        }

        if (LeaseNeedsRestore(out var serial, out var fingerprint))
        {
            return CompleteRestore(serial, fingerprint, _restoreGpu, reason, generation: null, stopWatch: true);
        }

        return LastOutcome ?? new ThermalSessionOutcome
        {
            Summary = string.IsNullOrWhiteSpace(reason)
                ? "No owned experimental GPU override was active."
                : reason
        };
    }

    public ThermalSessionOutcome? RecoverSnapshot(string serial, string? fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        lock (_gate)
        {
            if (_cancel is not null)
            {
                return null;
            }

            _serial = serial;
            _fingerprint = fingerprint;
            _blocked = true;
            _pending = true;
            _pendingSummary = "A stored headset snapshot still tracks experimental GPU level 7 without an active watch.";
            SaveLatch(serial, _pendingSummary, PropertyTransactionStatus.Incomplete, failed: true, owned: true);
        }

        var outcome = CompleteRestore(
            serial,
            fingerprint,
            _restoreGpu,
            "Startup found a stored GPU level 7 snapshot without an active thermal watch. " + ExperimentalGpuThermalPolicy.GateDisclaimer,
            generation: null,
            stopWatch: true);
        if (outcome.RestorationStatus == PropertyTransactionStatus.Incomplete
            || (outcome.RestorationStatus == PropertyTransactionStatus.Failed
                && outcome.Summary.Contains("could not be read", StringComparison.OrdinalIgnoreCase)))
        {
            EnsureRecoveryLoop(serial, fingerprint, outcome.Summary);
        }

        return outcome;
    }

    public ThermalSessionOutcome? RecoverPendingLease()
    {
        if (!LeaseNeedsRestore(out var serial, out var fingerprint))
        {
            return null;
        }

        var outcome = CompleteRestore(
            serial,
            fingerprint,
            _restoreGpu,
            "Startup found an experimental GPU override that was still owned. Restoration was attempted. " + ExperimentalGpuThermalPolicy.GateDisclaimer,
            generation: null,
            stopWatch: true);
        if (outcome.RestorationStatus == PropertyTransactionStatus.Incomplete
            || (outcome.RestorationStatus == PropertyTransactionStatus.Failed
                && (outcome.Summary.Contains("could not be read", StringComparison.OrdinalIgnoreCase)
                    || outcome.Summary.Contains("in progress", StringComparison.OrdinalIgnoreCase)
                    || outcome.Summary.Contains("Thermal restoration failed", StringComparison.OrdinalIgnoreCase))))
        {
            EnsureRecoveryLoop(serial, fingerprint, outcome.Summary);
        }

        return outcome;
    }

    public ThermalSessionOutcome Run(
        string serial,
        string? fingerprint,
        Func<string, ThermalReading> read,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        Func<DateTimeOffset> clock,
        Action<TimeSpan, CancellationToken> wait,
        CancellationToken cancellation,
        Func<bool>? stillOwner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(restoreGpu);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(wait);
        if (AutomaticReapplyBlocked && stillOwner is null)
        {
            return new ThermalSessionOutcome
            {
                BlockedAutomaticReapply = true,
                Summary = "Automatic reapplication of GPU level 7 is blocked after a thermal restoration. Apply it again from the Headset page if you still want it. " + ExperimentalGpuThermalPolicy.GateDisclaimer
            };
        }

        var samples = 0;
        while (samples < ExperimentalGpuThermalPolicy.MaxSamplesPerSession)
        {
            if (cancellation.IsCancellationRequested || OwnerEnded(stillOwner))
            {
                return EndOwnedWatch(serial, fingerprint, restoreGpu, stillOwner, cancellation);
            }

            wait(ExperimentalGpuThermalPolicy.SampleInterval, cancellation);
            if (cancellation.IsCancellationRequested || OwnerEnded(stillOwner))
            {
                return EndOwnedWatch(serial, fingerprint, restoreGpu, stillOwner, cancellation);
            }

            samples++;
            ThermalReading reading;
            try
            {
                reading = read(serial);
            }
            catch (Exception ex)
            {
                reading = ThermalReading.Unreachable(clock(), ex.GetType().Name);
            }

            var decision = ExperimentalGpuThermalPolicy.Evaluate(reading, clock());
            if (decision.Action == ThermalGuardAction.Continue)
            {
                continue;
            }

            var restored = CompleteRestore(serial, fingerprint, restoreGpu, decision.Reason, generation: null, stopWatch: false);
            if (restored.Restored || restored.RestorationStatus == PropertyTransactionStatus.Conflict || cancellation.IsCancellationRequested)
            {
                return restored;
            }

            return restored;
        }

        return CompleteRestore(
            serial,
            fingerprint,
            restoreGpu,
            "The experimental GPU watch reached its sample budget and is restoring the original property. " + ExperimentalGpuThermalPolicy.GateDisclaimer,
            generation: null,
            stopWatch: false);
    }

    public void Dispose() => RestoreOwnedAndStop("Experimental GPU thermal watch disposed.", TimeSpan.FromSeconds(5));

    private void Watch(int generation, string serial, string? fingerprint, CancellationTokenSource started, CancellationToken token)
    {
        ThermalSessionOutcome outcome;
        try
        {
            outcome = Run(serial, fingerprint, _read, _restoreGpu, () => DateTimeOffset.UtcNow, Wait, token, () => IsGeneration(generation));
            while (ShouldRetry(outcome) && IsGeneration(generation) && !_shutdown)
            {
                Wait(ExperimentalGpuThermalPolicy.SampleInterval, token);
                if (!IsGeneration(generation))
                {
                    break;
                }

                outcome = CompleteRestore(
                    serial,
                    fingerprint,
                    _restoreGpu,
                    "Retrying restoration of the owned experimental GPU override. " + ExperimentalGpuThermalPolicy.GateDisclaimer,
                    generation,
                    stopWatch: false);
            }
        }
        catch (Exception ex)
        {
            outcome = IsGeneration(generation)
                ? CompleteRestore(serial, fingerprint, _restoreGpu, "The thermal watch failed (" + ex.GetType().Name + "). " + ExperimentalGpuThermalPolicy.GateDisclaimer, generation, stopWatch: false)
                : Superseded();
            _log?.Invoke("Experimental GPU thermal watch failed: " + ex.GetType().Name);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cancel, started))
                {
                    _cancel.Dispose();
                    _cancel = null;
                }
                else
                {
                    started.Dispose();
                }
            }
        }

        Publish(outcome);
        _log?.Invoke(outcome.Summary);
    }

    private void EnsureRecoveryLoop(string serial, string? fingerprint, string reason)
    {
        CancellationToken token;
        CancellationTokenSource started;
        int generation;
        lock (_gate)
        {
            if (_shutdown)
            {
                return;
            }

            _generation++;
            generation = _generation;
            _suspendRestore = false;
            _serial = serial;
            _fingerprint = fingerprint;
            _cancel?.Cancel();
            _cancel = new CancellationTokenSource();
            started = _cancel;
            token = started.Token;
            SaveLatch(serial, reason, PropertyTransactionStatus.Incomplete, failed: true, owned: true);
        }

        var task = Task.Run(() =>
        {
            var outcome = new ThermalSessionOutcome
            {
                RestorationPending = true,
                BlockedAutomaticReapply = true,
                Summary = reason
            };
            try
            {
                while (IsGeneration(generation) && !_shutdown)
                {
                    outcome = CompleteRestore(serial, fingerprint, _restoreGpu, reason, generation, stopWatch: false);
                    if (outcome.Restored || outcome.RestorationStatus == PropertyTransactionStatus.Conflict || !IsGeneration(generation) || _shutdown)
                    {
                        break;
                    }

                    Wait(ExperimentalGpuThermalPolicy.SampleInterval, token);
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke("Experimental GPU restoration retry failed: " + ex.GetType().Name);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_cancel, started))
                    {
                        _cancel.Dispose();
                        _cancel = null;
                    }
                    else
                    {
                        started.Dispose();
                    }
                }
            }

            Publish(outcome);
        });
        lock (_gate)
        {
            _task = task;
        }
    }

    private ThermalSessionOutcome EndOwnedWatch(
        string serial,
        string? fingerprint,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        Func<bool>? stillOwner,
        CancellationToken cancellation)
    {
        if (stillOwner is not null && !stillOwner())
        {
            return Superseded();
        }

        var outcome = CompleteRestore(
            serial,
            fingerprint,
            restoreGpu,
            "The experimental GPU watch ended. The owned override is being restored so it cannot remain active without this watch. " + ExperimentalGpuThermalPolicy.GateDisclaimer,
            generation: null,
            stopWatch: true);
        return new ThermalSessionOutcome
        {
            Restored = outcome.Restored,
            Cancelled = cancellation.IsCancellationRequested,
            BlockedAutomaticReapply = true,
            RestorationPending = outcome.RestorationPending,
            RestorationStatus = outcome.RestorationStatus,
            Summary = outcome.Summary
        };
    }

    private static bool OwnerEnded(Func<bool>? stillOwner) => stillOwner is not null && !stillOwner();

    private static bool ShouldRetry(ThermalSessionOutcome outcome) =>
        !outcome.Restored
        && outcome.RestorationStatus is PropertyTransactionStatus.Failed or PropertyTransactionStatus.Incomplete
        && !outcome.Summary.Contains("superseded", StringComparison.OrdinalIgnoreCase);

    private bool IsGeneration(int generation)
    {
        lock (_gate) return _generation == generation && !_suspendRestore;
    }

    private void Publish(ThermalSessionOutcome outcome)
    {
        lock (_gate)
        {
            LastOutcome = outcome;
        }
    }

    private static ThermalSessionOutcome Superseded() => new()
    {
        Cancelled = true,
        Summary = "Experimental GPU thermal watch superseded. This watch did not restore."
    };

    private ThermalSessionOutcome CompleteRestore(
        string serial,
        string? fingerprint,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        string reason,
        int? generation,
        bool stopWatch)
    {
        lock (_gate)
        {
            if (_suspendRestore || (generation is int expected && expected != _generation))
            {
                return Superseded();
            }
        }

        PropertyTransactionResult result;
        try
        {
            result = restoreGpu(serial, fingerprint);
        }
        catch (Exception ex)
        {
            result = new PropertyTransactionResult
            {
                Status = PropertyTransactionStatus.Failed,
                Summary = "Thermal restoration failed (" + ex.GetType().Name + ")."
            };
        }

        var failed = result.Status != PropertyTransactionStatus.Restored && result.Status != PropertyTransactionStatus.Conflict;
        var pending = failed || result.Status == PropertyTransactionStatus.Conflict;
        lock (_gate)
        {
            if (_suspendRestore || (generation is int expected && expected != _generation))
            {
                return Superseded();
            }

            _blocked = true;
            _pending = pending && result.Status != PropertyTransactionStatus.Restored;
            _serial = serial;
            _fingerprint = fingerprint;
            _pendingSummary = pending
                ? reason + " Restoration is pending. Status: " + result.Status + ". " + result.Summary
                : null;
            SaveLatch(serial, reason, result.Status, failed || result.Status == PropertyTransactionStatus.Conflict, owned: failed);
            if (stopWatch)
            {
                _cancel?.Cancel();
            }
        }

        var summary = reason + " Restoration status: " + result.Status + ". " + result.Summary;
        summary += failed
            ? " The failure was recorded. Automatic reapplication stays blocked."
            : " Automatic reapplication stays blocked until an explicit Headset page apply.";
        var outcome = new ThermalSessionOutcome
        {
            Restored = result.Status == PropertyTransactionStatus.Restored,
            RestorationStatus = result.Status,
            BlockedAutomaticReapply = true,
            RestorationPending = failed,
            Summary = summary
        };
        Publish(outcome);
        return outcome;
    }

    private static void Wait(TimeSpan delay, CancellationToken cancellation)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero)
        {
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            var slice = remaining > TimeSpan.FromMilliseconds(200) ? TimeSpan.FromMilliseconds(200) : remaining;
            Thread.Sleep(slice);
            remaining -= slice;
        }
    }

    private bool LeaseNeedsRestore(out string serial, out string? fingerprint)
    {
        serial = string.Empty;
        fingerprint = null;
        var latch = ReadLatch();
        if (latch is null || string.IsNullOrWhiteSpace(latch.Serial))
        {
            return false;
        }

        if (!latch.Owned)
        {
            return false;
        }

        serial = latch.Serial;
        fingerprint = latch.Fingerprint;
        return true;
    }

    private bool LeaseSaysOwned()
    {
        var latch = ReadLatch();
        return latch is not null && (latch.Owned || latch.RestorationFailed);
    }

    private bool LoadLatch()
    {
        var latch = ReadLatch();
        if (latch is null)
        {
            return _latchPath is not null && File.Exists(_latchPath);
        }

        _pending = latch.RestorationFailed || latch.Owned;
        _pendingSummary = latch.Reason;
        _serial = latch.Serial;
        _fingerprint = latch.Fingerprint;
        return latch.Blocked || latch.Owned || latch.RestorationFailed;
    }

    private ThermalLatchFile? ReadLatch()
    {
        if (_latchPath is null || !File.Exists(_latchPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ThermalLatchFile>(File.ReadAllText(_latchPath));
        }
        catch (JsonException)
        {
            _pending = true;
            _pendingSummary = "The thermal recovery file is unreadable. Automatic GPU level 7 reapplication stays blocked and nothing was written.";
            return null;
        }
    }

    private void SaveOwnedLease(string serial, string? fingerprint)
    {
        SaveLatch(serial, "Experimental GPU level 7 is owned by an active thermal watch.", PropertyTransactionStatus.Verified, failed: false, owned: true, blocked: false);
        _ = fingerprint;
    }

    private void SaveLatch(string serial, string reason, PropertyTransactionStatus status, bool failed, bool owned, bool blocked = true)
    {
        if (_latchPath is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_latchPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _latchPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new ThermalLatchFile
            {
                Blocked = blocked,
                Owned = owned,
                RestorationFailed = failed,
                Serial = serial,
                Fingerprint = _fingerprint,
                Reason = reason,
                RestorationStatus = status.ToString()
            }));
            if (File.Exists(_latchPath))
            {
                File.Replace(temp, _latchPath, null);
            }
            else
            {
                File.Move(temp, _latchPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke("Could not persist the thermal restoration latch (" + ex.GetType().Name + "). The in-memory block still applies.");
        }
    }

    private void DeleteLatch()
    {
        if (_latchPath is null || !File.Exists(_latchPath))
        {
            return;
        }

        try
        {
            File.Delete(_latchPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class ThermalLatchFile
    {
        public bool Blocked { get; set; }
        public bool Owned { get; set; }
        public bool RestorationFailed { get; set; }
        public string? Serial { get; set; }
        public string? Fingerprint { get; set; }
        public string? Reason { get; set; }
        public string? RestorationStatus { get; set; }
    }
}
