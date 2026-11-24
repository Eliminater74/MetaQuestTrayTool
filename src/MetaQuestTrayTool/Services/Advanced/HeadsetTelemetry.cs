namespace MetaQuestTrayTool.Services;

public enum MetricQuality
{
    Measured,
    Estimated,
    Inferred
}

public enum MetricAvailability
{
    Available,
    Unavailable
}

public sealed record TelemetryMetric(
    string Name,
    string? Unit,
    double? Value,
    string? Text,
    MetricQuality Quality,
    MetricAvailability Availability,
    string Source,
    string? Provider,
    DateTimeOffset TimestampUtc,
    string? Note)
{
    public static TelemetryMetric AvailableNumber(string name, string unit, double value, MetricQuality quality, string source, string? provider, DateTimeOffset timestamp, string? note = null) =>
        new(name, unit, value, null, quality, MetricAvailability.Available, source, provider, timestamp, note);

    public static TelemetryMetric Unavailable(string name, string? unit, string source, string? provider, DateTimeOffset timestamp, string note) =>
        new(name, unit, null, null, MetricQuality.Measured, MetricAvailability.Unavailable, source, provider, timestamp, note);
}

public static class TelemetryCollectionPolicy
{
    public static bool AllowHeadsetAdb(bool pcvrSessionActive, bool userRequested) =>
        userRequested || pcvrSessionActive;

    public static bool AllowBackgroundAdbWhileIdle => false;
}

public static class HeadsetTelemetry
{
    public static IReadOnlyList<TelemetryMetric> FromSample(HeadsetPerformanceSample sample, DateTimeOffset timestamp, string? provider)
    {
        var last = sample.Samples.LastOrDefault();
        var metrics = new List<TelemetryMetric>();
        Add(metrics, "RuntimeFps", "FPS", last?.Fps, timestamp, provider,
            "Runtime log FPS. This is not fresh-frame video delivery unless the source says so.");
        Add(metrics, "RefreshHz", "Hz", last?.RefreshHz, timestamp, provider, null);
        Add(metrics, "HeadsetCpuUtilization", "%", last?.CpuUtilizationPercent, timestamp, provider, null);
        Add(metrics, "HeadsetGpuUtilization", "%", last?.GpuUtilizationPercent, timestamp, provider, null);
        Add(metrics, "HeadsetCpuLevel", "level", last?.CpuLevel, timestamp, provider, null);
        Add(metrics, "HeadsetGpuLevel", "level", last?.GpuLevel, timestamp, provider, null);
        Add(metrics, "HeadsetCpuClock", "MHz", last?.CpuClockMHz, timestamp, provider, null);
        Add(metrics, "HeadsetGpuClock", "MHz", last?.GpuClockMHz, timestamp, provider, null);
        Add(metrics, "HeadsetMemoryClock", "MHz", last?.MemoryClockMHz, timestamp, provider, null);
        Add(metrics, "HeadsetBatteryTemperature", "C", last?.BatteryTemperatureC, timestamp, provider, null);
        Add(metrics, "DroppedFrames", "frames", last?.DroppedFrames, timestamp, provider, null);
        Add(metrics, "ReprojectedFrames", "frames", last?.ReprojectedFrames, timestamp, provider, null);
        metrics.Add(last?.ThermalStatus is { Length: > 0 } thermal
            ? new TelemetryMetric("ThermalStatus", null, null, thermal, MetricQuality.Measured, MetricAvailability.Available, "headset-logcat", provider, timestamp, null)
            : TelemetryMetric.Unavailable("ThermalStatus", null, "headset-logcat", provider, timestamp, "No thermal field in the sample."));
        metrics.Add(last?.Foveation is { Length: > 0 } foveation
            ? new TelemetryMetric("Foveation", null, null, foveation, MetricQuality.Measured, MetricAvailability.Available, "headset-logcat", provider, timestamp, null)
            : TelemetryMetric.Unavailable("Foveation", null, "headset-logcat", provider, timestamp, "No foveation field in the sample."));
        metrics.Add(last?.SpaceWarp is { Length: > 0 } spaceWarp
            ? new TelemetryMetric("SpaceWarp", null, null, spaceWarp, MetricQuality.Measured, MetricAvailability.Available, "headset-logcat", provider, timestamp, null)
            : TelemetryMetric.Unavailable("SpaceWarp", null, "headset-logcat", provider, timestamp, "No SpaceWarp field in the sample."));
        return metrics;
    }

    private static void Add(List<TelemetryMetric> metrics, string name, string unit, double? value, DateTimeOffset timestamp, string? provider, string? note)
    {
        metrics.Add(value is double number
            ? TelemetryMetric.AvailableNumber(name, unit, number, MetricQuality.Measured, "headset-logcat", provider, timestamp, note)
            : TelemetryMetric.Unavailable(name, unit, "headset-logcat", provider, timestamp, "Field was not present in the runtime sample."));
    }
}
