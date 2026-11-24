using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MetaQuestTrayTool.Services;

public enum BenchmarkDisruption
{
    NonDisruptive,
    RequiresReconnect,
    RequiresSteamVrRestart,
    Unsupported
}

public sealed class BenchmarkSessionResult
{
    public int DurationSeconds { get; init; }
    public int WarmupSeconds { get; init; }
    public bool Cancelled { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public string Metric { get; init; } = "FPS";
    public BenchmarkDisruption Disruption { get; init; } = BenchmarkDisruption.NonDisruptive;
    public BenchmarkReport Report { get; init; } = new();
    public string EnvironmentNote { get; init; } = "Headset temperature and room conditions were not measured.";
    public string RestoreNote { get; init; } = "No settings were changed.";

    public string ToDisplayText() => string.Join(Environment.NewLine, [
        $"Duration: {DurationSeconds.ToString(CultureInfo.InvariantCulture)} seconds",
        $"Warm-up discarded: {WarmupSeconds.ToString(CultureInfo.InvariantCulture)} seconds",
        $"Cancelled: {Cancelled}",
        $"Disruption: {Disruption}",
        Report.ToDisplayText(),
        EnvironmentNote,
        RestoreNote
    ]);
}

public static class BenchmarkSession
{
    public const int MaxStoredRuns = 20;

    public static BenchmarkDisruption Classify(string comparison) => comparison.Trim().ToLowerInvariant() switch
    {
        "telemetry" or "pc-gpu" => BenchmarkDisruption.NonDisruptive,
        "refresh" or "gpu-level" or "bitrate" => BenchmarkDisruption.RequiresReconnect,
        "steamvr-runtime" => BenchmarkDisruption.RequiresSteamVrRestart,
        _ => BenchmarkDisruption.Unsupported
    };

    public static string GuardComparison(BenchmarkDisruption disruption, bool authorized, Action? proceed)
    {
        if (disruption == BenchmarkDisruption.Unsupported)
        {
            return "Unsupported comparison. Nothing was changed.";
        }

        if (disruption != BenchmarkDisruption.NonDisruptive && !authorized)
        {
            return "Refused. The comparison can interrupt the session and was not authorized. Nothing was changed.";
        }

        proceed?.Invoke();
        return disruption == BenchmarkDisruption.NonDisruptive
            ? "Non-disruptive comparison started. SteamVR was not restarted."
            : "Authorized comparison started. The caller must restore the original settings snapshot.";
    }

    public static BenchmarkSessionResult Run(
        int durationSeconds,
        int warmupSeconds,
        Func<DateTimeOffset> clock,
        Func<double?> sample,
        Action<TimeSpan> wait,
        CancellationToken cancellation,
        string metric = "FPS")
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(wait);
        if (durationSeconds is not (30 or 60 or 300))
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Benchmark durations are 30, 60, or 300 seconds.");
        }

        if (warmupSeconds is < 0 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupSeconds));
        }

        var started = clock();
        var deadline = started + TimeSpan.FromSeconds(durationSeconds + warmupSeconds);
        var samples = new List<double>();
        var cancelled = false;
        var guard = 0;
        while (clock() < deadline)
        {
            if (++guard > 10_000)
            {
                break;
            }

            if (cancellation.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var now = clock();
            var value = sample();
            if (value is double sampleValue && double.IsFinite(sampleValue) && now - started >= TimeSpan.FromSeconds(warmupSeconds))
            {
                samples.Add(sampleValue);
            }

            var remaining = deadline - clock();
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            wait(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1));
        }

        return new BenchmarkSessionResult
        {
            DurationSeconds = durationSeconds,
            WarmupSeconds = warmupSeconds,
            Cancelled = cancelled,
            StartedUtc = started,
            Metric = metric,
            Disruption = BenchmarkDisruption.NonDisruptive,
            Report = BenchmarkMath.FromPeriodicSamples(metric, samples),
            EnvironmentNote = "Host-side sample only. Headset temperature and room conditions were not measured.",
            RestoreNote = "No headset, Link, or SteamVR setting was changed, so there was nothing to restore."
        };
    }
}

public static class BenchmarkHistoryStore
{
    public static IReadOnlyList<BenchmarkSessionResult> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<BenchmarkSessionResult>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Append(string path, BenchmarkSessionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(result);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var history = Read(path).TakeLast(BenchmarkSession.MaxStoredRuns - 1).Append(result).ToList();
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path))
        {
            File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    public static string ToJson(BenchmarkSessionResult result) =>
        JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

    public static string ToCsv(BenchmarkSessionResult result)
    {
        var report = result.Report;
        return string.Join(',', [
            "duration_seconds",
            "warmup_seconds",
            "cancelled",
            "metric",
            "samples",
            "average",
            "median",
            "minimum",
            "maximum",
            "one_percent_low"
        ]) + Environment.NewLine + string.Join(',', [
            result.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            result.WarmupSeconds.ToString(CultureInfo.InvariantCulture),
            result.Cancelled ? "true" : "false",
            report.Metric,
            report.SampleCount.ToString(CultureInfo.InvariantCulture),
            Format(report.Average),
            Format(report.Median),
            Format(report.Minimum),
            Format(report.Maximum),
            Format(report.OnePercentLow)
        ]);
    }

    private static string Format(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unavailable";
}
