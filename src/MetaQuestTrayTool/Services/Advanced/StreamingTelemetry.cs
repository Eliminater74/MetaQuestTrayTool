using System.Globalization;
using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public static class StreamingTelemetry
{
    private static readonly (string Name, string Unit, string Pattern, bool FreshFrame)[] Fields =
    [
        ("StreamingBitrate", "Mbps", @"(?i)\bbitrate\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*(?:mbps|mbit/s|mbit)?", false),
        ("EncodeLatency", "ms", @"(?i)\bencode(?:\s*latency)?\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*ms\b", false),
        ("DecodeLatency", "ms", @"(?i)\bdecode(?:\s*latency)?\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*ms\b", false),
        ("NetworkLatency", "ms", @"(?i)\bnetwork(?:\s*latency)?\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*ms\b", false),
        ("TransportLatency", "ms", @"(?i)\btransport(?:\s*latency)?\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*ms\b", false),
        ("PacketLoss", "%", @"(?i)\bpacket\s*loss\s*[=:]\s*(?<n>\d+(?:\.\d+)?)\s*%?", false),
        ("FreshFrameFps", "FPS", @"(?i)\bfresh(?:\s*frame)?\s*fps\s*[=:]\s*(?<n>\d+(?:\.\d+)?)", true)
    ];

    public static IReadOnlyList<TelemetryMetric> Parse(string? text, string? provider, DateTimeOffset timestamp)
    {
        var source = text ?? string.Empty;
        var estimated = source.Contains("estimate", StringComparison.OrdinalIgnoreCase);
        var metrics = new List<TelemetryMetric>();
        foreach (var field in Fields)
        {
            var match = Regex.Match(source, field.Pattern);
            if (!match.Success || !double.TryParse(match.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                metrics.Add(TelemetryMetric.Unavailable(field.Name, field.Unit, "streaming-log", provider, timestamp, "The active provider log did not include this field."));
                continue;
            }

            var quality = estimated && !field.FreshFrame ? MetricQuality.Estimated : MetricQuality.Measured;
            var note = field.FreshFrame
                ? "Labeled fresh-frame FPS from the provider log. Not optical motion-to-photon latency."
                : estimated ? "The source text marked this latency as an estimate." : null;
            metrics.Add(TelemetryMetric.AvailableNumber(field.Name, field.Unit, value, quality, "streaming-log", provider, timestamp, note));
        }

        return metrics;
    }
}
