namespace MetaQuestTrayTool.Models;

public sealed class HeadsetIdentity
{
    public string? Serial { get; init; }
    public string? AdbSerial { get; init; }
    public string? Model { get; init; }
    public string? Device { get; init; }
    public string? Manufacturer { get; init; }
    public string? AndroidVersion { get; init; }
    public string? BuildDisplay { get; init; }
    public string? Fingerprint { get; init; }
    public string? State { get; init; }
    public bool IsReady { get; init; }
    public bool IsVrHeadset { get; init; }
    public bool IsIgnored { get; init; }
    public string? IgnoreReason { get; init; }
    public bool IsTrusted { get; init; }
    public bool IsRogue { get; init; }
    public HeadsetRuntimeStatus? Runtime { get; init; }

    public string Summary
    {
        get
        {
            if (IsIgnored)
            {
                return IgnoreReason ?? "Ignored ADB device — not a VR headset.";
            }

            if (string.IsNullOrWhiteSpace(AdbSerial) && string.IsNullOrWhiteSpace(Serial))
            {
                return "none (OK for Air Link)";
            }

            var name = Model ?? Device ?? "Headset";
            var serial = Serial ?? AdbSerial ?? "?";
            var trust = IsRogue ? "BLOCKED (not the trusted headset)" : IsTrusted ? "trusted" : "not yet trusted";
            var runtime = Runtime is { Available: true } ? $"  ·  {Runtime.Summary}" : "";
            return $"{name}  ·  {serial}  ·  {trust}{runtime}";
        }
    }

    /// <summary>Info-page ADB line, optionally aware of an active Link / streamer session.</summary>
    public string DescribeAdbBanner(VrConnectionStatus? link = null)
    {
        if (IsIgnored)
        {
            return IgnoreReason ?? "Ignored ADB device — not a VR headset.";
        }

        if (IsReady && IsVrHeadset)
        {
            var name = Model ?? Device ?? "Headset";
            var serial = Serial ?? AdbSerial ?? "?";
            var trust = IsRogue ? "BLOCKED" : IsTrusted ? "trusted" : "not trusted yet";
            var runtime = Runtime is { Available: true } ? $" · {Runtime.Summary}" : "";
            return $"{name} · {serial} · {trust} · ADB {State ?? "device"}{runtime}";
        }

        if (link?.SessionActive == true
            && link.Kind is VrConnectionKind.MetaAirLink
                or VrConnectionKind.MetaWiredLink
                or VrConnectionKind.SteamLinkOrSteamVr
                or VrConnectionKind.VirtualDesktop)
        {
            return link.Kind switch
            {
                VrConnectionKind.MetaAirLink =>
                    "not connected — normal on Air Link. Plug USB + Developer Mode (or wireless ADB) only for headset ADB tweaks.",
                VrConnectionKind.MetaWiredLink =>
                    "not connected — enable USB debugging on the Quest if you want ADB tweaks during Link.",
                VrConnectionKind.SteamLinkOrSteamVr =>
                    "not connected — normal for Steam Link. Use USB/wireless ADB only for headset props.",
                VrConnectionKind.VirtualDesktop =>
                    "not connected — normal for Virtual Desktop. Use USB/wireless ADB only for headset props.",
                _ => "not connected"
            };
        }

        return "not connected — enable Developer Mode + USB (or wireless ADB) for headset tweaks.";
    }
}
