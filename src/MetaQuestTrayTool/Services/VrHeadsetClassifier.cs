using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// ADB commands and auto-apply only run on standalone VR headsets.
/// Phones, tablets, Android emulators, and unknown gadgets are ignored.
/// </summary>
public static class VrHeadsetClassifier
{
    private static readonly string[] EmulatorTokens =
    [
        "emulator", "qemu", "goldfish", "ranchu", "sdk_google", "sdk_gphone",
        "google_atv", "atv64", "sdk_gtablet", "generic_x86", "android sdk",
        "android_x86", "vbox86", "vsoc", "cuttlefish"
    ];

    private static readonly string[] MetaTokens =
    [
        "quest", "oculus", "hollywood", "eureka", "pacific", "seacliff",
        "quest_3s", "quest 3s", "horizon os"
    ];

    private static readonly string[] SamsungXrTokens =
    [
        "galaxy xr", "galaxyxr", "moohan", "android xr", "androidxr"
    ];

    private static readonly string[] HtcVrTokens =
    [
        "vive focus", "xr elite", "xrelite", "focus vision", "vive xr", "focus3", "focus 3"
    ];

    private static readonly string[] PicoTokens =
    [
        "pico 3", "pico 4", "pico neo", "pico4", "pico g3", "pico g2"
    ];

    private static readonly string[] SteamFrameTokens =
    [
        "steam frame", "steamframe", "steam headset"
    ];

    public static bool LooksLikeNonHeadsetSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return false;
        }

        return serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase)
               || serial.StartsWith("EMULATOR", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsObviousEmulator(AdbDevice device)
    {
        if (LooksLikeNonHeadsetSerial(device.Serial))
        {
            return true;
        }

        return ContainsAny(Blob(device), EmulatorTokens);
    }

    public static bool IsAllowedVrHeadset(AdbDevice device, DeviceProbe? probe = null)
    {
        if (IsObviousEmulator(device))
        {
            return false;
        }

        var blob = $"{Blob(device)} {probe?.Blob}".Trim();
        if (ContainsAny(blob, EmulatorTokens) || probe?.IsEmulator == true)
        {
            return false;
        }

        if (ContainsAny(blob, MetaTokens) || !string.IsNullOrWhiteSpace(probe?.OculusDevice))
        {
            return true;
        }

        // Quest 3S ADB product is "panther" — same Pixel 7 codename. Only allow with Meta/Quest context.
        if (Contains(blob, "panther") && (Contains(blob, "meta") || Contains(blob, "oculus") || Contains(blob, "quest")))
        {
            return true;
        }

        if (ContainsAny(blob, SamsungXrTokens))
        {
            return true;
        }

        if (ContainsAny(blob, HtcVrTokens) || (Contains(blob, "htc") && Contains(blob, "vive")))
        {
            return true;
        }

        if (ContainsAny(blob, PicoTokens) || Contains(blob, "manufacturer:pico") || EndsWithManufacturer(probe, "pico"))
        {
            return true;
        }

        if (ContainsAny(blob, SteamFrameTokens))
        {
            return true;
        }

        return false;
    }

    public static string DescribeIgnored(AdbDevice device, DeviceProbe? probe = null)
    {
        var name = probe?.Model ?? device.Model ?? device.Product ?? device.Serial;
        if (IsObviousEmulator(device) || probe?.IsEmulator == true)
        {
            return $"{name} (emulator) — ignored. Only a real VR headset can receive commands.";
        }

        return $"{name} — not a VR headset (phone/tablet/other). Commands will not run.";
    }

    public static string AllowedHeadsetList =>
        "Quest 2 / 3 / 3S / Pro, Samsung Galaxy XR, HTC Vive standalone, Pico, or Steam Frame";

    private static bool EndsWithManufacturer(DeviceProbe? probe, string name) =>
        probe?.Manufacturer?.Equals(name, StringComparison.OrdinalIgnoreCase) == true;

    private static string Blob(AdbDevice device) =>
        $"{device.Serial} {device.Model} {device.Product} {device.DeviceName}";

    private static bool ContainsAny(string blob, IEnumerable<string> tokens) =>
        tokens.Any(token => Contains(blob, token));

    private static bool Contains(string blob, string token) =>
        blob.Contains(token, StringComparison.OrdinalIgnoreCase);
}

public sealed class DeviceProbe
{
    public string? Model { get; init; }
    public string? Device { get; init; }
    public string? Manufacturer { get; init; }
    public string? Brand { get; init; }
    public string? Hardware { get; init; }
    public string? Fingerprint { get; init; }
    public string? OculusDevice { get; init; }
    public bool IsEmulator { get; init; }

    public string Blob =>
        $"{Model} {Device} {Manufacturer} {Brand} {Hardware} {Fingerprint} {OculusDevice}";
}
