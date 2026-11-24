using System.Globalization;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class HeadsetAdbProbe
{
    public string? Serial { get; init; }
    public string? ModelFallback { get; init; }
    public string? GetPropDump { get; init; }
    public string? DisplayDump { get; init; }
    public string? BatteryDump { get; init; }
    public bool AdbUnavailable { get; init; }
    public string? AdbError { get; init; }
}

public static class HeadsetCapabilityDiscovery
{
    private static readonly Regex GetPropLine = new(
        @"^\[(?<key>[^\]]+)\]:\s*\[(?<value>.*)\]\s*$",
        RegexOptions.Compiled);

    private static readonly Regex RefreshValue = new(
        @"(?i)\b(?:refresh(?:\s*rate)?|fps)\s*[=:]\s*(?<n>\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    private static readonly Regex ModeSize = new(
        @"\b(?<w>\d{3,4})x(?<h>\d{3,4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BatteryTemperature = new(
        @"(?im)^\s*temperature:\s*(?<n>-?\d+)\s*$",
        RegexOptions.Compiled);

    private static readonly string[] HorizonKeys =
    [
        "ro.oculus.build.version",
        "ro.build.version.incremental",
        "ro.build.display.id"
    ];

    public static HeadsetCapabilityReport Discover(HeadsetAdbProbe? probe, DateTimeOffset? capturedUtc = null)
    {
        var props = ParseGetProp(probe?.GetPropDump);
        var model = First(props, "ro.product.model") ?? probe?.ModelFallback;
        var family = HeadsetFamilyClassifier.Classify(model);
        var notes = new List<string>();
        if (probe?.AdbUnavailable == true)
        {
            notes.Add("ADB snapshot unavailable. Model fallback only. " + (probe.AdbError ?? string.Empty).Trim());
        }

        if (string.IsNullOrWhiteSpace(probe?.GetPropDump) && probe?.AdbUnavailable != true)
        {
            notes.Add("No getprop dump was supplied. Standard refresh uses the model table.");
        }

        string? horizon = null;
        string? horizonSource = null;
        foreach (var key in HorizonKeys)
        {
            var value = First(props, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                horizon = value;
                horizonSource = key;
                break;
            }
        }

        var standard = StandardRates(model);
        var currentRefresh = ParseRefresh(probe?.DisplayDump);
        var modes = ParseModes(probe?.DisplayDump);
        var temperature = ParseTemperature(probe?.BatteryDump);
        var gpuProperty = First(props, "debug.oculus.gpuLevel");

        return new HeadsetCapabilityReport
        {
            Serial = string.IsNullOrWhiteSpace(probe?.Serial) ? null : probe!.Serial.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? null : HeadsetFamilyClassifier.Normalize(model),
            Family = family,
            AndroidVersion = First(props, "ro.build.version.release"),
            BuildFingerprint = First(props, "ro.build.fingerprint"),
            HorizonOsVersion = horizon,
            HorizonOsSource = horizonSource,
            StandardRefreshRatesHz = standard,
            StandardRefreshSource = standard.Count == 0
                ? (string.IsNullOrWhiteSpace(model) ? CapabilityState.Unknown : CapabilityState.DocumentedModel)
                : CapabilityState.DocumentedModel,
            ObservedDisplayModes = modes,
            CurrentRefreshHz = currentRefresh,
            CurrentRefreshState = currentRefresh is null
                ? (probe?.AdbUnavailable == true ? CapabilityState.TemporarilyUnavailable : CapabilityState.Unknown)
                : CapabilityState.Verified,
            GpuLevel7 = GpuLevel7State(family),
            GpuLevelProperty = gpuProperty,
            GpuLevelPropertyState = gpuProperty is null
                ? (probe?.AdbUnavailable == true ? CapabilityState.TemporarilyUnavailable : CapabilityState.Unknown)
                : CapabilityState.Verified,
            ThermalReadable = temperature is not null,
            BatteryTemperatureC = temperature,
            ThermalState = temperature is not null
                ? CapabilityState.Verified
                : (probe?.AdbUnavailable == true ? CapabilityState.TemporarilyUnavailable : CapabilityState.Unknown),
            PerformanceTelemetry = probe?.AdbUnavailable == true
                ? CapabilityState.TemporarilyUnavailable
                : CapabilityState.DocumentedModel,
            CapturedUtc = capturedUtc ?? DateTimeOffset.UtcNow,
            Notes = notes
        };
    }

    public static CapabilityState GpuLevel7State(HeadsetFamily family) => family switch
    {
        HeadsetFamily.Quest3 => CapabilityState.Experimental,
        HeadsetFamily.Unknown => CapabilityState.Unknown,
        _ => CapabilityState.Unsupported
    };

    public static IReadOnlyList<int> StandardRates(string? model)
    {
        var rates = new List<int>();
        foreach (var rate in HeadsetCapabilities.RefreshRates(model))
        {
            var hz = rate switch
            {
                HeadsetRefreshRate.Hz60 => 60,
                HeadsetRefreshRate.Hz72 => 72,
                HeadsetRefreshRate.Hz80 => 80,
                HeadsetRefreshRate.Hz90 => 90,
                HeadsetRefreshRate.Hz120 => 120,
                _ => (int?)null
            };
            if (hz is int value && !rates.Contains(value))
            {
                rates.Add(value);
            }
        }

        return rates;
    }

    internal static Dictionary<string, string> ParseGetProp(string? dump)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(dump))
        {
            return result;
        }

        foreach (var raw in dump.Replace("\r", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var match = GetPropLine.Match(raw.Trim());
            if (!match.Success)
            {
                continue;
            }

            result[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static string? First(Dictionary<string, string> props, string key) =>
        props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static double? ParseRefresh(string? dump)
    {
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        var match = RefreshValue.Match(dump);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value is > 0 and < 1000
            ? value
            : null;
    }

    private static IReadOnlyList<string> ParseModes(string? dump)
    {
        if (string.IsNullOrWhiteSpace(dump))
        {
            return [];
        }

        var modes = new List<string>();
        foreach (Match match in ModeSize.Matches(dump))
        {
            var mode = match.Value;
            if (!modes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            {
                modes.Add(mode);
            }
        }

        return modes;
    }

    internal static double? ParseTemperature(string? dump)
    {
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        var match = BatteryTemperature.Match(dump);
        if (!match.Success || !int.TryParse(match.Groups["n"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tenths))
        {
            return null;
        }

        return tenths / 10.0;
    }
}

public sealed class HeadsetCapabilityCache
{
    private HeadsetCapabilityReport? _cached;

    public HeadsetCapabilityReport? Current => _cached;

    public void Invalidate() => _cached = null;

    public HeadsetCapabilityReport GetOrAdd(string? serial, string? fingerprint, Func<HeadsetCapabilityReport> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_cached is not null
            && Same(serial, _cached.Serial)
            && Same(fingerprint, _cached.BuildFingerprint))
        {
            return _cached;
        }

        var created = factory();
        _cached = created;
        return created;
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.Ordinal);
}
