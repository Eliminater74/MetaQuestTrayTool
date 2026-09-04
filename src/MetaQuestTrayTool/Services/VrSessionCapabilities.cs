using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Which tray features make sense for the current PCVR transport.
/// Virtual Desktop / Steam Link do not use Meta's Link registry or OculusDebugToolCLI pipeline.
/// </summary>
public sealed class VrSessionCapabilities
{
    public required VrConnectionKind Kind { get; init; }
    public bool SessionActive { get; init; }
    public bool AllowsMetaLinkRegistry { get; init; }
    public bool AllowsOculusDebugTool { get; init; }
    public bool AllowsOpenXrSwitch { get; init; } = true;
    public bool AllowsHeadsetAdb { get; init; } = true;
    public string? Banner { get; init; }

    public bool RestrictsMetaPipeline => !AllowsMetaLinkRegistry || !AllowsOculusDebugTool;

    public string MetaLinkSkipMessage =>
        $"Quest Link live apply skipped — {StreamerLabel} uses its own bitrate/codec settings.";

    public string OdtSkipMessage =>
        $"Oculus Debug Tool (SS / ASW / FOV) skipped — not applied under {StreamerLabel}.";

    public string StreamerLabel => Kind switch
    {
        VrConnectionKind.VirtualDesktop => "Virtual Desktop",
        VrConnectionKind.SteamLinkOrSteamVr => "Steam Link / SteamVR",
        _ => "this non-Meta streamer"
    };

    public static VrSessionCapabilities From(VrConnectionStatus status)
    {
        var nonMeta = status.SessionActive
                      && status.Kind is VrConnectionKind.VirtualDesktop
                          or VrConnectionKind.SteamLinkOrSteamVr;

        if (!nonMeta)
        {
            return new VrSessionCapabilities
            {
                Kind = status.Kind,
                SessionActive = status.SessionActive,
                AllowsMetaLinkRegistry = true,
                AllowsOculusDebugTool = true,
                Banner = null
            };
        }

        if (status.Kind == VrConnectionKind.VirtualDesktop)
        {
            return new VrSessionCapabilities
            {
                Kind = status.Kind,
                SessionActive = true,
                AllowsMetaLinkRegistry = false,
                AllowsOculusDebugTool = false,
                Banner =
                    "Virtual Desktop session detected — live Meta Link registry and Oculus Debug Tool (SS / ASW / FOV) apply are paused for this session. "
                    + "You can still edit and save Quest Link presets for the next Meta Link / Air Link session. "
                    + "Headset ADB, OpenXR, power, and audio still work. Change bitrate/codec in Virtual Desktop's own settings."
            };
        }

        return new VrSessionCapabilities
        {
            Kind = status.Kind,
            SessionActive = true,
            AllowsMetaLinkRegistry = false,
            AllowsOculusDebugTool = false,
            Banner =
                "Steam Link / SteamVR session detected — live Meta Link registry and Oculus Debug Tool (SS / ASW / FOV) apply are paused for this session. "
                + "You can still edit and save Quest Link presets for the next Meta Link / Air Link session. "
                + "Use Steam Link (in-headset) and SteamVR Video settings for bitrate and resolution. "
                + "OpenXR should be SteamVR for OpenXR games. Headset ADB, power, and audio still work."
        };
    }
}
