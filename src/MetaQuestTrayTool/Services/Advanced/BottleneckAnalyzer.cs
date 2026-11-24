namespace MetaQuestTrayTool.Services;

public enum FindingConfidence
{
    Insufficient,
    Low,
    Medium,
    High
}

public sealed record BottleneckFinding(
    string Code,
    string Title,
    string Explanation,
    FindingConfidence Confidence,
    IReadOnlyList<string> Evidence)
{
    public string ToDisplayText() =>
        $"{Title} ({Confidence})" + Environment.NewLine
        + Explanation + Environment.NewLine
        + string.Join(Environment.NewLine, Evidence.Select(item => "- " + item));
}

public static class BottleneckAnalyzer
{
    public static IReadOnlyList<BottleneckFinding> Analyze(IReadOnlyList<TelemetryMetric> metrics)
    {
        metrics ??= [];
        var findings = new List<BottleneckFinding>();
        var headsetGpu = Value(metrics, "HeadsetGpuUtilization");
        var pcGpu = Value(metrics, "PcGpuUtilization");
        var pcCpu = Value(metrics, "PcCpuUtilization");
        var headsetCpu = Value(metrics, "HeadsetCpuUtilization");
        var encoder = Value(metrics, "PcVideoEncoderUtilization");
        var temp = Value(metrics, "HeadsetBatteryTemperature");
        var reprojected = Value(metrics, "ReprojectedFrames");
        var dropped = Value(metrics, "DroppedFrames");
        var network = Value(metrics, "NetworkLatency");
        var decode = Value(metrics, "DecodeLatency");
        var fps = Value(metrics, "RuntimeFps");
        var refresh = Value(metrics, "RefreshHz");
        var thermal = metrics.FirstOrDefault(metric => metric.Name == "ThermalStatus" && metric.Availability == MetricAvailability.Available)?.Text;

        if (headsetGpu is >= 95 && pcGpu is < 80)
        {
            findings.Add(Possible("headset-gpu", "Possible headset GPU bottleneck",
                "The headset GPU sample is near saturation while the PC GPU sample still has headroom. This can also be a short spike.",
                FindingConfidence.Medium,
                [$"Headset GPU {headsetGpu:0.#}%", $"PC GPU {pcGpu:0.#}%"]));
        }
        else if (headsetGpu is >= 95 && pcGpu is null)
        {
            findings.Add(Possible("headset-gpu", "Possible headset GPU pressure",
                "The headset GPU sample is near saturation. The PC GPU was not sampled, so this is not a comparison.",
                FindingConfidence.Low,
                [$"Headset GPU {headsetGpu:0.#}%"]));
        }

        if (pcGpu is >= 95 && (headsetGpu is null || headsetGpu < 90))
        {
            findings.Add(Possible("pc-gpu", "Possible PC GPU bottleneck",
                "The PC GPU sample is near saturation. A game can still be CPU-limited inside a single thread even when this average is lower.",
                FindingConfidence.Medium,
                [$"PC GPU {pcGpu:0.#}%"]));
        }

        if (pcCpu is >= 95 && (pcGpu is null || pcGpu < 70))
        {
            findings.Add(Possible("pc-cpu", "Possible PC CPU bottleneck",
                "PC processor utilization is very high while the PC GPU sample is not saturated.",
                FindingConfidence.Medium,
                [$"PC CPU {pcCpu:0.#}%"]));
        }

        if (encoder is >= 90)
        {
            findings.Add(Possible("encoder", "Possible video encoder pressure",
                "The GPU video encoder sample is high. This does not identify which streaming app is encoding.",
                FindingConfidence.Medium,
                [$"Encoder {encoder:0.#}%"]));
        }

        if (temp is >= 45 || (thermal?.Contains("throttl", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            findings.Add(Possible("thermal", "Possible thermal pressure",
                "Temperature or a thermal field crossed the advisory threshold. Firmware limits are not overridden.",
                FindingConfidence.Medium,
                [temp is double c ? $"Battery {c:0.0} C" : "Temperature not numeric", $"Thermal field: {thermal ?? "not present"}"]));
        }

        if (reprojected is > 0 || dropped is > 0)
        {
            findings.Add(Possible("reprojection", "Reprojection or dropped-frame counters were non-zero",
                "These counters came from the runtime log. They are not a full frame-time percentile.",
                FindingConfidence.Low,
                [$"Reprojected {reprojected?.ToString("0") ?? "not present"}", $"Dropped {dropped?.ToString("0") ?? "not present"}"]));
        }

        if (network is > 40)
        {
            findings.Add(Possible("network", "Possible network latency pressure",
                "The provider log reported network latency above 40 ms. That number is not motion-to-photon latency and does not by itself prove congestion.",
                network is > 80 ? FindingConfidence.Medium : FindingConfidence.Low,
                [$"Network latency {network:0.#} ms"]));
        }

        if (decode is > 12)
        {
            findings.Add(Possible("decode", "Possible decode workload",
                "Decode time from the provider log is high for the sampled line. The source may be an estimate.",
                FindingConfidence.Low,
                [$"Decode {decode:0.#} ms"]));
        }

        if (fps is double delivered && refresh is double target && delivered < target * 0.85)
        {
            findings.Add(Possible("pace", "Runtime FPS is below the sampled refresh",
                "Runtime FPS is below 85% of the sampled refresh. This is not proof of the delivered video frame rate.",
                FindingConfidence.Low,
                [$"Runtime FPS {delivered:0.#}", $"Refresh {target:0.#} Hz"]));
        }

        if (findings.Count == 0)
        {
            var evidence = metrics
                .Where(metric => metric.Availability == MetricAvailability.Available && metric.Value is not null)
                .Take(8)
                .Select(metric => $"{metric.Name} {metric.Value:0.##} {metric.Unit}")
                .ToList();
            findings.Add(evidence.Count < 2
                ? new BottleneckFinding("insufficient", "Insufficient diagnostic evidence",
                    "Not enough measured utilization or latency fields were available to compare bottlenecks.",
                    FindingConfidence.Insufficient, evidence.Count == 0 ? ["No available numeric metrics."] : evidence)
                : new BottleneckFinding("none", "No advisory threshold was crossed",
                    "The available samples did not meet a bottleneck rule. This is not a certification of the session.",
                    FindingConfidence.Low, evidence));
        }
        else if (findings.Count > 1)
        {
            findings.Insert(0, new BottleneckFinding(
                "multiple",
                "More than one possible cause",
                "These findings can be true at the same time. None of them is a definitive root cause.",
                FindingConfidence.Low,
                findings.Select(finding => finding.Title).ToList()));
        }

        return findings;
    }

    public static string ToDisplayText(IReadOnlyList<BottleneckFinding> findings) =>
        string.Join(Environment.NewLine + Environment.NewLine, findings.Select(finding => finding.ToDisplayText()));

    private static BottleneckFinding Possible(string code, string title, string explanation, FindingConfidence confidence, IReadOnlyList<string> evidence) =>
        new(code, title, explanation, confidence, evidence);

    private static double? Value(IReadOnlyList<TelemetryMetric> metrics, string name) =>
        metrics.FirstOrDefault(metric => metric.Name == name && metric.Availability == MetricAvailability.Available && metric.Value is not null)?.Value;
}
