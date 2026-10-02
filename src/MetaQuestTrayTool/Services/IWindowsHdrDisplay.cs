namespace MetaQuestTrayTool.Services;

/// <summary>One active Windows display target, identified without changing it.</summary>
public readonly record struct HdrDisplayId(long AdapterLuid, uint TargetId)
{
    public static long PackAdapter(uint lowPart, int highPart) => ((long)highPart << 32) | lowPart;

    public uint AdapterLowPart => unchecked((uint)AdapterLuid);

    public int AdapterHighPart => unchecked((int)(AdapterLuid >> 32));
}

/// <summary>Advanced-color state for one active display target.</summary>
public sealed record HdrDisplayState(
    HdrDisplayId Id,
    string FriendlyName,
    bool AdvancedColorSupported,
    bool AdvancedColorEnabled);

/// <summary>
/// Read/write seam for Windows advanced color. Tests supply a fake so they never
/// change the developer machine's HDR state.
/// </summary>
public interface IWindowsHdrDisplay
{
    bool TryQueryActive(out IReadOnlyList<HdrDisplayState> displays, out string? error);

    bool TrySetAdvancedColor(HdrDisplayId id, bool enabled, out string? error);
}

public readonly record struct HdrActionResult(bool Succeeded, string Summary)
{
    public static HdrActionResult Ok(string summary) => new(true, summary);

    public static HdrActionResult Warn(string summary) => new(false, summary);
}
