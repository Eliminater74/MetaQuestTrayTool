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
    public PropertyTransactionStatus? RestorationStatus { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public interface IExperimentalGpuSession
{
    bool AutomaticReapplyBlocked { get; }
    void BeginExplicit(string serial, string? model, string? fingerprint);
    void StopWithoutRestore(string reason);
}

public sealed class ExperimentalGpuThermalGuard : IExperimentalGpuSession, IDisposable
{
    private readonly Func<string, ThermalReading> _read;
    private readonly Func<string, string?, PropertyTransactionResult> _restoreGpu;
    private readonly Action<string>? _log;
    private readonly string? _latchPath;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancel;
    private bool _blocked;
    private string? _serial;
    private string? _fingerprint;

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
            DeleteLatch();
        }
    }

    public void BeginExplicit(string serial, string? model, string? fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        CancellationToken token;
        CancellationTokenSource started;
        lock (_gate)
        {
            _blocked = false;
            DeleteLatch();
            _serial = serial;
            _fingerprint = fingerprint;
            _cancel?.Cancel();
            _cancel?.Dispose();
            _cancel = new CancellationTokenSource();
            started = _cancel;
            token = started.Token;
        }

        _log?.Invoke("Experimental GPU thermal watch started for " + serial + ". " + ExperimentalGpuThermalPolicy.GateDisclaimer);
        _ = model;
        Task.Run(() =>
        {
            var outcome = Run(serial, fingerprint, _read, _restoreGpu, () => DateTimeOffset.UtcNow, Wait, token);
            lock (_gate)
            {
                LastOutcome = outcome;
                if (ReferenceEquals(_cancel, started))
                {
                    _cancel.Dispose();
                    _cancel = null;
                }
            }

            _log?.Invoke(outcome.Summary);
        });
    }

    public void StopWithoutRestore(string reason)
    {
        lock (_gate)
        {
            _cancel?.Cancel();
            _cancel?.Dispose();
            _cancel = null;
            _serial = null;
            _fingerprint = null;
        }

        _log?.Invoke(string.IsNullOrWhiteSpace(reason)
            ? "Experimental GPU thermal watch stopped without a thermal restoration."
            : reason);
    }

    public ThermalSessionOutcome Run(
        string serial,
        string? fingerprint,
        Func<string, ThermalReading> read,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        Func<DateTimeOffset> clock,
        Action<TimeSpan, CancellationToken> wait,
        CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(restoreGpu);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(wait);
        if (AutomaticReapplyBlocked)
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
            if (cancellation.IsCancellationRequested)
            {
                return new ThermalSessionOutcome
                {
                    Cancelled = true,
                    Summary = "Experimental GPU thermal watch cancelled. The override was not restored by cancellation. " + ExperimentalGpuThermalPolicy.GateDisclaimer
                };
            }

            wait(ExperimentalGpuThermalPolicy.SampleInterval, cancellation);
            if (cancellation.IsCancellationRequested)
            {
                return new ThermalSessionOutcome
                {
                    Cancelled = true,
                    Summary = "Experimental GPU thermal watch cancelled. The override was not restored by cancellation. " + ExperimentalGpuThermalPolicy.GateDisclaimer
                };
            }

            samples++;
            ThermalReading reading;
            try
            {
                reading = read(serial);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
                reading = ThermalReading.Unreachable(clock(), ex.GetType().Name);
            }

            var decision = ExperimentalGpuThermalPolicy.Evaluate(reading, clock());
            if (decision.Action == ThermalGuardAction.Continue)
            {
                continue;
            }

            return CompleteRestore(serial, fingerprint, restoreGpu, decision.Reason);
        }

        return CompleteRestore(
            serial,
            fingerprint,
            restoreGpu,
            "The experimental GPU watch reached its sample budget and restored the original property. " + ExperimentalGpuThermalPolicy.GateDisclaimer);
    }

    public void Dispose() => StopWithoutRestore("Experimental GPU thermal watch disposed.");

    private ThermalSessionOutcome CompleteRestore(
        string serial,
        string? fingerprint,
        Func<string, string?, PropertyTransactionResult> restoreGpu,
        string reason)
    {
        PropertyTransactionResult result;
        try
        {
            result = restoreGpu(serial, fingerprint);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            result = new PropertyTransactionResult
            {
                Status = PropertyTransactionStatus.Failed,
                Summary = "Thermal restoration failed (" + ex.GetType().Name + ")."
            };
        }

        var failed = result.Status is not PropertyTransactionStatus.Restored;
        lock (_gate)
        {
            _blocked = true;
            _serial = serial;
            _fingerprint = fingerprint;
            SaveLatch(serial, reason, result.Status, failed);
            _cancel?.Cancel();
        }

        var summary = reason + " Restoration status: " + result.Status + ". " + result.Summary;
        if (failed)
        {
            summary += " The failure was recorded. Automatic reapplication stays blocked.";
        }
        else
        {
            summary += " Automatic reapplication stays blocked until an explicit Headset page apply.";
        }

        return new ThermalSessionOutcome
        {
            Restored = result.Status == PropertyTransactionStatus.Restored,
            RestorationStatus = result.Status,
            BlockedAutomaticReapply = true,
            Summary = summary
        };
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

    private bool LoadLatch()
    {
        if (_latchPath is null || !File.Exists(_latchPath))
        {
            return false;
        }

        try
        {
            var latch = JsonSerializer.Deserialize<ThermalLatchFile>(File.ReadAllText(_latchPath));
            return latch?.Blocked == true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private void SaveLatch(string serial, string reason, PropertyTransactionStatus status, bool failed)
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

            File.WriteAllText(_latchPath, JsonSerializer.Serialize(new ThermalLatchFile
            {
                Blocked = true,
                Serial = serial,
                Reason = reason,
                RestorationStatus = status.ToString(),
                RestorationFailed = failed
            }));
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
        public string? Serial { get; set; }
        public string? Reason { get; set; }
        public string? RestorationStatus { get; set; }
        public bool RestorationFailed { get; set; }
    }
}
