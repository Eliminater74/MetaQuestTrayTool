namespace MetaQuestTrayTool.Models;

public enum VrConnectionKind
{
    Idle = 0,
    MetaWiredLink,
    MetaAirLink,
    SteamLinkOrSteamVr,
    VirtualDesktop,
    MetaLinkUnknownTransport,
    Unknown
}

public sealed class VrConnectionStatus
{
    public required VrConnectionKind Kind { get; init; }
    public required string Summary { get; init; }
    public string? Detail { get; init; }
    public bool SessionActive { get; init; }
    public bool? IsUsingAirLink { get; init; }
    public string? HeadsetSerial { get; init; }
    public string? DeviceCacheConnectionState { get; init; }
    public bool UsbHeadsetPresent { get; init; }
    public bool MetaHmdReported { get; init; }
    public bool SteamVrRunning { get; init; }
    public bool VirtualDesktopRunning { get; init; }

    public string Describe()
    {
        if (string.IsNullOrWhiteSpace(Detail))
        {
            return Summary;
        }

        return $"{Summary} ({Detail})";
    }
}
