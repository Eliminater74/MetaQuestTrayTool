namespace MetaQuestTrayTool.Models;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

/// <summary>Rough PCVR encode / SS headroom for Link recommendations.</summary>
public enum GpuPerformanceTier
{
    Unknown,
    Entry,
    Mid,
    High,
    Ultra
}

public sealed class GpuAdapterInfo
{
    public required string Name { get; init; }
    public GpuVendor Vendor { get; init; }
    public GpuPerformanceTier Tier { get; init; }
    public long? DedicatedMemoryBytes { get; init; }
    public string? RegistryKey { get; init; }

    public string DedicatedMemoryLabel => DedicatedMemoryBytes is > 0
        ? $"{DedicatedMemoryBytes.Value / (1024d * 1024d * 1024d):0.#} GB"
        : "—";

    public string TierLabel => Tier switch
    {
        GpuPerformanceTier.Entry => "Entry",
        GpuPerformanceTier.Mid => "Mid",
        GpuPerformanceTier.High => "High",
        GpuPerformanceTier.Ultra => "Ultra",
        _ => "Unknown"
    };

    public string VendorLabel => Vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA",
        GpuVendor.Amd => "AMD",
        GpuVendor.Intel => "Intel",
        _ => "Unknown"
    };

    public string Summary => DedicatedMemoryBytes is > 0
        ? $"{Name} · {TierLabel} · {DedicatedMemoryLabel}"
        : $"{Name} · {TierLabel}";
}

public sealed class GpuRecommendation
{
    public required GpuAdapterInfo Adapter { get; init; }
    public required string LinkPresetName { get; init; }
    public required string GlobalPresetName { get; init; }
    public required LinkSettings Link { get; init; }
    public required GameSettings Game { get; init; }
    public required string Rationale { get; init; }

    public string Banner =>
        $"{Adapter.Summary} — recommended Link “{LinkPresetName}”, globals “{GlobalPresetName}”.";
}
