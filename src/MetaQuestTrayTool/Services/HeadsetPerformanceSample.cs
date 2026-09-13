using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public sealed record HeadsetPerformanceSample(
    TimeSpan Duration,
    int LinesScanned,
    IReadOnlyList<HeadsetPerformanceLogSample> Samples)
{
    public string ToDisplayText()
    {
        var text = new StringBuilder();
        text.AppendLine($"Headset performance sample ({Duration.TotalSeconds:0}s)");
        text.AppendLine($"Scanned {LinesScanned} logcat line(s); parsed {Samples.Count} VrApi/runtime stat line(s).");
        text.AppendLine("Evidence source: runtime logcat stats, not setprop read-back.");

        if (Samples.Count == 0)
        {
            text.AppendLine("No FPS, CPU/GPU, foveation, or SpaceWarp values were found. Start a VR app, make sure it is rendering, then try again; some apps/firmware do not emit these fields in logcat.");
            return text.ToString().TrimEnd();
        }

        var fpsValues = Samples.Select(sample => sample.Fps).Where(value => value.HasValue).Select(value => value!.Value).ToList();
        if (fpsValues.Count > 0)
        {
            text.AppendLine($"FPS: last {fpsValues[^1].ToString("0.##", CultureInfo.InvariantCulture)}, average {fpsValues.Average().ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        AppendLast(text, "CPU level", Samples.Select(sample => sample.CpuLevel));
        AppendLast(text, "GPU level", Samples.Select(sample => sample.GpuLevel));
        AppendLast(text, "CPU utilization", Samples.Select(sample => sample.CpuUtilizationPercent), "%");
        AppendLast(text, "GPU utilization", Samples.Select(sample => sample.GpuUtilizationPercent), "%");
        AppendLast(text, "FFR/Foveation", Samples.Select(sample => sample.Foveation));
        AppendLast(text, "SpaceWarp", Samples.Select(sample => sample.SpaceWarp));

        var evidence = Samples.Select(sample => sample.RawLine).TakeLast(3).ToList();
        if (evidence.Count > 0)
        {
            text.AppendLine("Recent stat line(s):");
            foreach (var line in evidence)
            {
                text.AppendLine("  " + Truncate(line, 220));
            }
        }

        return text.ToString().TrimEnd();
    }

    internal static HeadsetPerformanceSample Parse(string logcat, TimeSpan duration)
    {
        var lines = (logcat ?? string.Empty)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var samples = new List<HeadsetPerformanceLogSample>();
        foreach (var line in lines)
        {
            var sample = HeadsetPerformanceLogSample.TryParse(line);
            if (sample is not null)
            {
                samples.Add(sample);
            }
        }

        return new HeadsetPerformanceSample(duration, lines.Length, samples);
    }

    private static void AppendLast(StringBuilder text, string label, IEnumerable<int?> values)
    {
        var value = values.LastOrDefault(item => item.HasValue);
        if (value.HasValue)
        {
            text.AppendLine($"{label}: {value.Value.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static void AppendLast(StringBuilder text, string label, IEnumerable<double?> values, string suffix)
    {
        var value = values.LastOrDefault(item => item.HasValue);
        if (value.HasValue)
        {
            text.AppendLine($"{label}: {value.Value.ToString("0.##", CultureInfo.InvariantCulture)}{suffix}");
        }
    }

    private static void AppendLast(StringBuilder text, string label, IEnumerable<string?> values)
    {
        var value = values.LastOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.AppendLine($"{label}: {value}");
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength] + "…";
}

public sealed record HeadsetPerformanceLogSample(
    string RawLine,
    double? Fps,
    int? CpuLevel,
    int? GpuLevel,
    double? CpuUtilizationPercent,
    double? GpuUtilizationPercent,
    string? Foveation,
    string? SpaceWarp)
{
    private static readonly Regex FpsRegex = new(@"\bFPS\s*[=:]\s*(?<value>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CpuLevelRegex = new(@"\b(?:CPU\s*Level|CpuLevel|cpuLevel|CPU)\s*[=:]\s*(?<value>\d+)\b(?!\s*%)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GpuLevelRegex = new(@"\b(?:GPU\s*Level|GpuLevel|gpuLevel|GPU)\s*[=:]\s*(?<value>\d+)\b(?!\s*%)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CpuUtilRegex = new(@"\b(?:CPU\s*(?:Util|Utilization)|CPUUtilization|CPUUtil|CPU%)\s*[=:]\s*(?<value>\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GpuUtilRegex = new(@"\b(?:GPU\s*(?:Util|Utilization)|GPUUtilization|GPUUtil|GPU%)\s*[=:]\s*(?<value>\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FoveationRegex = new(@"\b(?:Fov|Foveation|FFR)\s*[=:]\s*(?<value>[0-9]+D?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SpaceWarpRegex = new(@"\b(?:ASW|AppSW|SpaceWarp|SW)\s*[=:]\s*(?<value>[A-Za-z0-9_.\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static HeadsetPerformanceLogSample? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var fps = MatchDouble(FpsRegex, line);
        var cpuLevel = MatchInt(CpuLevelRegex, line);
        var gpuLevel = MatchInt(GpuLevelRegex, line);
        var cpuUtilization = MatchDouble(CpuUtilRegex, line);
        var gpuUtilization = MatchDouble(GpuUtilRegex, line);
        var foveation = MatchString(FoveationRegex, line);
        var spaceWarp = MatchString(SpaceWarpRegex, line);
        if (fps is null
            && cpuLevel is null
            && gpuLevel is null
            && cpuUtilization is null
            && gpuUtilization is null
            && string.IsNullOrWhiteSpace(foveation)
            && string.IsNullOrWhiteSpace(spaceWarp))
        {
            return null;
        }

        return new HeadsetPerformanceLogSample(
            line.Trim(),
            fps,
            cpuLevel,
            gpuLevel,
            cpuUtilization,
            gpuUtilization,
            foveation,
            spaceWarp);
    }

    private static double? MatchDouble(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? MatchInt(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? MatchString(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups["value"].Value : null;
    }
}
