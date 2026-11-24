using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace MetaQuestTrayTool.Services;

public static class PcTelemetry
{
    public static IReadOnlyList<TelemetryMetric> FromProbe(
        string? nvidiaSmiCsv,
        string? probeError,
        double? processorUtilizationPercent,
        DateTimeOffset timestamp)
    {
        var metrics = new List<TelemetryMetric>();
        AddOptional(metrics, "PcCpuUtilization", "%", processorUtilizationPercent, timestamp, "Windows processor counter");
        if (!string.IsNullOrWhiteSpace(probeError) || string.IsNullOrWhiteSpace(nvidiaSmiCsv))
        {
            var note = string.IsNullOrWhiteSpace(probeError)
                ? "No GPU probe output. Vendor tools are optional and are not required."
                : probeError.Trim();
            foreach (var name in new[] { "PcGpuUtilization", "PcGpuTemperature", "PcGpuClock", "PcGpuMemoryUsed", "PcGpuMemoryTotal", "PcVideoEncoderUtilization" })
            {
                metrics.Add(TelemetryMetric.Unavailable(name, null, "nvidia-smi", null, timestamp, note));
            }

            metrics.Add(TelemetryMetric.Unavailable("SteamVrFrameTime", "ms", "openvr", null, timestamp,
                "SteamVR frame timing is not read. An untested OpenVR binding is not loaded."));
            return metrics;
        }

        var line = nvidiaSmiCsv.Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(item => item.Contains(',', StringComparison.Ordinal));
        var parts = line?.Split(',').Select(part => part.Trim()).ToArray() ?? [];
        AddParsed(metrics, "PcGpuUtilization", "%", parts, 0, timestamp);
        AddParsed(metrics, "PcGpuTemperature", "C", parts, 1, timestamp);
        AddParsed(metrics, "PcGpuClock", "MHz", parts, 2, timestamp);
        AddParsed(metrics, "PcGpuMemoryUsed", "MiB", parts, 3, timestamp);
        AddParsed(metrics, "PcGpuMemoryTotal", "MiB", parts, 4, timestamp);
        AddParsed(metrics, "PcVideoEncoderUtilization", "%", parts, 5, timestamp);
        metrics.Add(TelemetryMetric.Unavailable("SteamVrFrameTime", "ms", "openvr", null, timestamp,
            "SteamVR frame timing is not read. An untested OpenVR binding is not loaded."));
        return metrics;
    }

    public static string? TryQueryNvidiaSmi(string? fileName = "nvidia-smi", int timeoutMs = 2000)
    {
        if (!IsSafeExecutable(fileName))
        {
            throw new InvalidOperationException("The GPU probe executable was rejected.");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName!,
                    Arguments = "--query-gpu=utilization.gpu,temperature.gpu,clocks.gr,memory.used,memory.total,utilization.encoder --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
            {
                return null;
            }

            if (!process.WaitForExit(Math.Clamp(timeoutMs, 200, 5000)))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
    }

    internal static bool IsSafeExecutable(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var name = Path.GetFileName(fileName.Trim());
        return name.Equals("nvidia-smi", StringComparison.OrdinalIgnoreCase)
            || name.Equals("nvidia-smi.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddOptional(List<TelemetryMetric> metrics, string name, string unit, double? value, DateTimeOffset timestamp, string source)
    {
        metrics.Add(value is double number
            ? TelemetryMetric.AvailableNumber(name, unit, number, MetricQuality.Measured, source, null, timestamp, null)
            : TelemetryMetric.Unavailable(name, unit, source, null, timestamp, "Not sampled."));
    }

    private static void AddParsed(List<TelemetryMetric> metrics, string name, string unit, string[] parts, int index, DateTimeOffset timestamp)
    {
        if (index < parts.Length
            && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !parts[index].Equals("[N/A]", StringComparison.OrdinalIgnoreCase)
            && !parts[index].Contains("N/A", StringComparison.OrdinalIgnoreCase))
        {
            metrics.Add(TelemetryMetric.AvailableNumber(name, unit, value, MetricQuality.Measured, "nvidia-smi", null, timestamp, "First GPU line only."));
            return;
        }

        metrics.Add(TelemetryMetric.Unavailable(name, unit, "nvidia-smi", null, timestamp, "GPU probe did not include this field."));
    }
}
