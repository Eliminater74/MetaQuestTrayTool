namespace MetaQuestTrayTool.Services;

/// <summary>
/// One capability discovery result. Requested settings are not stored here.
/// Values parsed from ADB are observations, not proof that a streaming app can deliver them.
/// </summary>
public sealed class HeadsetCapabilityReport
{
    public string? Serial { get; init; }
    public string? Model { get; init; }
    public HeadsetFamily Family { get; init; }
    public string? AndroidVersion { get; init; }
    public string? BuildFingerprint { get; init; }
    public string? HorizonOsVersion { get; init; }
    public string? HorizonOsSource { get; init; }
    public IReadOnlyList<int> StandardRefreshRatesHz { get; init; } = [];
    public CapabilityState StandardRefreshSource { get; init; }
    public IReadOnlyList<string> ObservedDisplayModes { get; init; } = [];
    public double? CurrentRefreshHz { get; init; }
    public CapabilityState CurrentRefreshState { get; init; }
    public CapabilityState GpuLevel7 { get; init; }
    public string? GpuLevelProperty { get; init; }
    public CapabilityState GpuLevelPropertyState { get; init; }
    public bool ThermalReadable { get; init; }
    public double? BatteryTemperatureC { get; init; }
    public CapabilityState ThermalState { get; init; }
    public CapabilityState PerformanceTelemetry { get; init; }
    public DateTimeOffset CapturedUtc { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];

    public string ToDisplayText()
    {
        var refresh = StandardRefreshRatesHz.Count == 0
            ? "none beyond device default"
            : string.Join(", ", StandardRefreshRatesHz);
        var current = CurrentRefreshHz is double hz
            ? hz.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " Hz"
            : CurrentRefreshState.ToString();
        var temp = BatteryTemperatureC is double c
            ? c.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " C"
            : ThermalState.ToString();
        return string.Join(Environment.NewLine, [
            $"Headset {Model ?? "unknown"} ({Family}), serial {(string.IsNullOrWhiteSpace(Serial) ? "not provided" : "present")}",
            $"Android {AndroidVersion ?? "unknown"}; Horizon/build {HorizonOsVersion ?? "unknown"} ({HorizonOsSource ?? "not read"})",
            $"Standard refresh ({StandardRefreshSource}): {refresh}",
            $"Current refresh: {current}",
            $"GPU level 7: {GpuLevel7}. Property readback: {GpuLevelProperty ?? GpuLevelPropertyState.ToString()}",
            $"Battery temperature: {temp}",
            $"Performance telemetry: {PerformanceTelemetry}",
            ..Notes
        ]);
    }
}
