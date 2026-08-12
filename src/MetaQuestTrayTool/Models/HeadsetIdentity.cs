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
                return "No VR headset on ADB.";
            }

            var name = Model ?? Device ?? "Headset";
            var serial = Serial ?? AdbSerial ?? "?";
            var trust = IsRogue ? "BLOCKED (not the trusted headset)" : IsTrusted ? "trusted" : "not yet trusted";
            return $"{name}  ·  {serial}  ·  {trust}";
        }
    }
}
