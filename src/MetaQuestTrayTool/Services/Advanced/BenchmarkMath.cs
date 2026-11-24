using System.Globalization;

namespace MetaQuestTrayTool.Services;

public enum BenchmarkPreset
{
    Quick = 30,
    Standard = 60,
    Extended = 300
}

public sealed class BenchmarkReport
{
    public string Metric { get; init; } = "FPS";
    public int SampleCount { get; init; }
    public bool FrameLevel { get; init; }
    public double? Average { get; init; }
    public double? Median { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public double? OnePercentLow { get; init; }
    public double? PointOnePercentLow { get; init; }
    public string PercentileNote { get; init; } = string.Empty;

    public string ToDisplayText() => string.Join(Environment.NewLine, [
        $"{Metric} samples: {SampleCount.ToString(CultureInfo.InvariantCulture)}",
        $"Frame-level data: {FrameLevel}",
        $"Average: {Format(Average)}",
        $"Median: {Format(Median)}",
        $"Minimum: {Format(Minimum)}",
        $"Maximum: {Format(Maximum)}",
        $"1% low: {Format(OnePercentLow)}",
        $"0.1% low: {Format(PointOnePercentLow)}",
        PercentileNote
    ]);

    private static string Format(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unavailable";
}

public static class BenchmarkMath
{
    public static int DurationSeconds(BenchmarkPreset preset) => (int)preset;

    public static int ClampCustomDuration(int seconds) => Math.Clamp(seconds, 5, 300);

    public static BenchmarkReport FromPeriodicFps(IReadOnlyList<double> fpsSamples) =>
        FromValues("FPS", fpsSamples, frameLevel: false);

    public static BenchmarkReport FromPeriodicSamples(string metric, IReadOnlyList<double> samples) =>
        FromValues(string.IsNullOrWhiteSpace(metric) ? "sample" : metric.Trim(), samples, frameLevel: false);

    public static BenchmarkReport FromFrameTimesMilliseconds(IReadOnlyList<double> frameTimesMs)
    {
        var fps = frameTimesMs.Where(time => time > 0).Select(time => 1000d / time).ToList();
        var report = FromValues("FPS", fps, frameLevel: true);
        var one = LowFps(frameTimesMs, 0.01);
        var tenth = frameTimesMs.Count >= 1000 ? LowFps(frameTimesMs, 0.001) : (double?)null;
        return new BenchmarkReport
        {
            Metric = report.Metric,
            SampleCount = frameTimesMs.Count,
            FrameLevel = true,
            Average = report.Average,
            Median = report.Median,
            Minimum = report.Minimum,
            Maximum = report.Maximum,
            OnePercentLow = frameTimesMs.Count >= 100 ? one : null,
            PointOnePercentLow = tenth,
            PercentileNote = frameTimesMs.Count >= 100
                ? "Lows are FPS from the slowest fraction of frame times."
                : "1% and 0.1% lows need at least 100 and 1000 frame times."
        };
    }

    public static string Compare(
        bool interruptsSession,
        bool authorized,
        Action applyA,
        Action applyB,
        Func<string> measure,
        Action restore)
    {
        ArgumentNullException.ThrowIfNull(applyA);
        ArgumentNullException.ThrowIfNull(applyB);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(restore);
        if (interruptsSession && !authorized)
        {
            return "Refused. The comparison would interrupt the VR session and was not authorized. SteamVR was not restarted and no game was closed.";
        }

        try
        {
            applyA();
            var a = measure();
            applyB();
            var b = measure();
            applyA();
            var a2 = measure();
            return "ABBA comparison completed." + Environment.NewLine + a + Environment.NewLine + b + Environment.NewLine + a2;
        }
        finally
        {
            restore();
        }
    }

    private static BenchmarkReport FromValues(string metric, IReadOnlyList<double> values, bool frameLevel)
    {
        var clean = values.Where(value => double.IsFinite(value)).OrderBy(value => value).ToList();
        if (clean.Count == 0)
        {
            return new BenchmarkReport
            {
                Metric = metric,
                FrameLevel = frameLevel,
                PercentileNote = "No finite samples."
            };
        }

        return new BenchmarkReport
        {
            Metric = metric,
            SampleCount = clean.Count,
            FrameLevel = frameLevel,
            Average = clean.Average(),
            Median = clean[clean.Count / 2],
            Minimum = clean[0],
            Maximum = clean[^1],
            OnePercentLow = null,
            PointOnePercentLow = null,
            PercentileNote = frameLevel
                ? "Frame-level FPS is present, but lows are calculated from frame times."
                : "1% and 0.1% lows are unavailable. Periodic FPS samples are too coarse."
        };
    }

    private static double? LowFps(IReadOnlyList<double> frameTimesMs, double fraction)
    {
        var slowest = frameTimesMs.Where(time => time > 0).OrderByDescending(time => time).ToList();
        if (slowest.Count == 0)
        {
            return null;
        }

        var count = Math.Max(1, (int)Math.Ceiling(slowest.Count * fraction));
        var averageMs = slowest.Take(count).Average();
        return averageMs <= 0 ? null : 1000d / averageMs;
    }
}
