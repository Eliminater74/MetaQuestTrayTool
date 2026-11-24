using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MetaQuestTrayTool.Services;

public enum ComparisonOrder
{
    Ab,
    Abba
}

public sealed class ComparisonRequest
{
    public string Setting { get; init; } = "telemetry";
    public string Baseline { get; init; } = "baseline";
    public string Candidate { get; init; } = "candidate";
    public BenchmarkDisruption Disruption { get; init; } = BenchmarkDisruption.NonDisruptive;
    public bool Authorized { get; init; }
    public ComparisonOrder Order { get; init; } = ComparisonOrder.Ab;
    public int DurationSeconds { get; init; } = 30;
    public int WarmupSeconds { get; init; }
    public int StabilizationSeconds { get; init; }
}

public sealed class ComparisonLeg
{
    public string Value { get; init; } = string.Empty;
    public BenchmarkReport Report { get; init; } = new();
    public double? TemperatureC { get; init; }
    public bool Cancelled { get; init; }
}

public sealed class ComparisonOutcome
{
    public bool Completed { get; init; }
    public bool Cancelled { get; init; }
    public bool Refused { get; init; }
    public bool Restored { get; init; }
    public bool RestoreFailed { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string ThermalNote { get; init; } = string.Empty;
    public string StatisticsNote { get; init; } = "1% and 0.1% lows are not calculated. These are periodic samples, not frame times.";
    public IReadOnlyList<ComparisonLeg> Legs { get; init; } = [];

    public string ToDisplayText() => string.Join(Environment.NewLine, new[]
    {
        Summary,
        ThermalNote,
        StatisticsNote
    }.Concat(Legs.Select(leg => leg.Value + ": " + leg.Report.ToDisplayText())));
}

public static class SettingsComparison
{
    public static ComparisonOutcome Run(
        ComparisonRequest request,
        Action<string> apply,
        Func<double?> sample,
        Func<double?>? temperature,
        Func<DateTimeOffset> clock,
        Action<TimeSpan> wait,
        Action restore,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(restore);

        if (request.Disruption is BenchmarkDisruption.RequiresSteamVrRestart or BenchmarkDisruption.Unsupported)
        {
            return Refuse("That comparison is not offered. SteamVR was not restarted and no setting was changed.");
        }

        if (request.Disruption != BenchmarkDisruption.NonDisruptive && !request.Authorized)
        {
            return Refuse("Refused. The comparison can interrupt the session and was not authorized. Nothing was changed.");
        }

        var legs = new List<ComparisonLeg>();
        string? failure = null;
        var cancelled = false;
        try
        {
            cancelled = SampleLeg(request, request.Baseline, apply, sample, temperature, clock, wait, cancellation, legs);
            if (!cancelled)
            {
                cancelled = SampleLeg(request, request.Candidate, apply, sample, temperature, clock, wait, cancellation, legs);
            }

            if (!cancelled && request.Order == ComparisonOrder.Abba)
            {
                cancelled = SampleLeg(request, request.Baseline, apply, sample, temperature, clock, wait, cancellation, legs);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            failure = ex.Message;
        }

        var restoreFailed = false;
        try
        {
            restore();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            restoreFailed = true;
            failure = (failure ?? "Comparison failed.") + " Restore failed: " + ex.Message;
        }

        var temps = legs.Select(leg => leg.TemperatureC).ToList();
        var thermal = DescribeThermal(temps);
        if (failure is not null)
        {
            return new ComparisonOutcome
            {
                Cancelled = cancelled,
                Restored = !restoreFailed,
                RestoreFailed = restoreFailed,
                Summary = failure,
                ThermalNote = thermal,
                Legs = legs
            };
        }

        return new ComparisonOutcome
        {
            Completed = !cancelled,
            Cancelled = cancelled,
            Restored = !restoreFailed,
            RestoreFailed = restoreFailed,
            Summary = cancelled
                ? "Comparison cancelled. The original setting was restored when a change had been requested."
                : request.Order + " comparison finished for " + request.Setting + ". SteamVR was not restarted.",
            ThermalNote = thermal,
            Legs = legs
        };
    }

    private static bool SampleLeg(
        ComparisonRequest request,
        string value,
        Action<string> apply,
        Func<double?> sample,
        Func<double?>? temperature,
        Func<DateTimeOffset> clock,
        Action<TimeSpan> wait,
        CancellationToken cancellation,
        List<ComparisonLeg> legs)
    {
        apply(value);
        if (request.StabilizationSeconds > 0)
        {
            wait(TimeSpan.FromSeconds(request.StabilizationSeconds));
        }

        if (cancellation.IsCancellationRequested)
        {
            return true;
        }

        var result = BenchmarkSession.Run(
            request.DurationSeconds,
            request.WarmupSeconds,
            clock,
            sample,
            wait,
            cancellation,
            request.Setting);
        legs.Add(new ComparisonLeg
        {
            Value = value,
            Report = result.Report,
            TemperatureC = temperature?.Invoke(),
            Cancelled = result.Cancelled
        });
        return result.Cancelled;
    }

    private static string DescribeThermal(IReadOnlyList<double?> temperatures)
    {
        if (temperatures.Count == 0 || temperatures.Any(value => value is null))
        {
            return "Thermal consistency: not established. At least one leg had no temperature sample. This is not a hardware safety limit.";
        }

        var values = temperatures.Select(value => value!.Value).ToList();
        var spread = values.Max() - values.Min();
        return spread > 5
            ? "Thermal consistency: battery readings differed by " + spread.ToString("0.0", CultureInfo.InvariantCulture) + " C. Conditions were not treated as matched. This is not a hardware safety limit."
            : "Thermal consistency: battery readings stayed within 5 C. This is not a hardware safety limit.";
    }

    private static ComparisonOutcome Refuse(string summary) => new()
    {
        Refused = true,
        Summary = summary,
        ThermalNote = "No temperature was sampled because the comparison did not start."
    };
}

public static class ComparisonHistory
{
    public const int MaxStored = 20;

    public static void Append(string path, ComparisonOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(outcome);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        List<ComparisonOutcome> history;
        try
        {
            history = File.Exists(path)
                ? JsonSerializer.Deserialize<List<ComparisonOutcome>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch (JsonException)
        {
            history = [];
        }

        history = history.TakeLast(MaxStored - 1).Append(outcome).ToList();
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(history));
        if (File.Exists(path))
        {
            File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }
}
