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

    /// <summary>Short banner line for the Info page (user-facing).</summary>
    public string InfoBanner
    {
        get
        {
            var serial = string.IsNullOrWhiteSpace(HeadsetSerial) ? null : HeadsetSerial;
            if (!SessionActive
                && Kind is VrConnectionKind.Idle or VrConnectionKind.Unknown
                or VrConnectionKind.MetaAirLink or VrConnectionKind.MetaWiredLink
                or VrConnectionKind.MetaLinkUnknownTransport)
            {
                return serial is null ? Summary : $"{Summary} · {serial}";
            }

            return Kind switch
            {
                VrConnectionKind.MetaAirLink when SessionActive =>
                    serial is null
                        ? "Meta Air Link — connected (wireless)"
                        : $"Meta Air Link — connected (wireless) · {serial}",
                VrConnectionKind.MetaWiredLink when SessionActive =>
                    serial is null
                        ? "Meta wired Link — connected (USB)"
                        : $"Meta wired Link — connected (USB) · {serial}",
                VrConnectionKind.SteamLinkOrSteamVr when SessionActive =>
                    "Steam Link / SteamVR — session active",
                VrConnectionKind.VirtualDesktop when SessionActive =>
                    "Virtual Desktop — session active",
                VrConnectionKind.MetaLinkUnknownTransport when SessionActive =>
                    "Meta Link — connected (transport unclear)",
                _ => serial is null ? Summary : $"{Summary} · {serial}"
            };
        }
    }
}
