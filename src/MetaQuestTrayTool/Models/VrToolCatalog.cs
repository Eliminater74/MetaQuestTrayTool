namespace MetaQuestTrayTool.Models;

public sealed class VrToolLink
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string Url { get; init; }
}

/// <summary>
/// Curated third-party VR utilities that complement Meta Quest Tray Tool.
/// Links open in the default browser — not affiliated with these projects.
/// </summary>
public static class VrToolCatalog
{
    public static IReadOnlyList<VrToolLink> All { get; } =
    [
        // Play more games
        new VrToolLink
        {
            Name = "Revive",
            Category = "Play more games",
            Summary = "Run many Meta / Oculus Store exclusives on SteamVR headsets (compatibility layer).",
            Url = "https://github.com/LibreVR/Revive/releases/latest"
        },
        new VrToolLink
        {
            Name = "UEVR",
            Category = "Play more games",
            Summary = "Inject VR into many Unreal Engine flatscreen games (community mod framework).",
            Url = "https://github.com/praydog/UEVR/releases/latest"
        },
        new VrToolLink
        {
            Name = "OculusKiller",
            Category = "Play more games",
            Summary = "Replace Oculus Dash with SteamVR when using Link — similar idea to this app’s Dash → SteamVR.",
            Url = "https://github.com/DevOculus-Meta-Quest/OculusKiller"
        },

        // Overlays & desktop in VR
        new VrToolLink
        {
            Name = "SteamVR Advanced Settings",
            Category = "Overlays & desktop in VR",
            Summary = "Chaperone, playspace, supersampling, bindings helpers, and other SteamVR dashboard tools.",
            Url = "https://store.steampowered.com/app/1009850/SteamVR_Advanced_Settings/"
        },
        new VrToolLink
        {
            Name = "OpenVR Advanced Settings (GitHub)",
            Category = "Overlays & desktop in VR",
            Summary = "Open-source Advanced Settings builds and source if you prefer GitHub releases.",
            Url = "https://github.com/OpenVR-Advanced-Settings/OpenVR-AdvancedSettings/releases/latest"
        },
        new VrToolLink
        {
            Name = "OVR Toolkit",
            Category = "Overlays & desktop in VR",
            Summary = "Desktop windows, Discord, browser, and keyboard in VR — popular SteamVR overlay suite.",
            Url = "https://store.steampowered.com/app/1068820/OVR_Toolkit/"
        },
        new VrToolLink
        {
            Name = "XSOverlay",
            Category = "Overlays & desktop in VR",
            Summary = "Lightweight SteamVR overlays for desktop, watch, and wrist widgets while you play.",
            Url = "https://store.steampowered.com/app/1173510/XSOverlay/"
        },
        new VrToolLink
        {
            Name = "Desktop+",
            Category = "Overlays & desktop in VR",
            Summary = "Free SteamVR desktop overlay with multi-monitor and window pinning.",
            Url = "https://store.steampowered.com/app/1163720/"
        },

        // Performance & diagnostics
        new VrToolLink
        {
            Name = "fpsVR",
            Category = "Performance & diagnostics",
            Summary = "In-headset FPS, frametime, CPU/GPU, and reprojection stats for SteamVR sessions.",
            Url = "https://store.steampowered.com/app/908520/fpsVR/"
        },
        new VrToolLink
        {
            Name = "CapFrameX",
            Category = "Performance & diagnostics",
            Summary = "Capture and analyze frame times — great for comparing SS / encode / GPU presets.",
            Url = "https://www.capframex.com/"
        },
        new VrToolLink
        {
            Name = "OpenXR Toolkit",
            Category = "Performance & diagnostics",
            Summary = "Upscaling, foveated overlay, and OpenXR tuning for supported apps (community project; check status).",
            Url = "https://mbucchia.github.io/OpenXR-Toolkit/"
        },

        // Wireless PCVR
        new VrToolLink
        {
            Name = "Virtual Desktop",
            Category = "Wireless PCVR",
            Summary = "Premium wireless PCVR streaming with strong SteamVR support — bitrate lives in VD, not Meta Link.",
            Url = "https://www.vrdesktop.net/"
        },
        new VrToolLink
        {
            Name = "ALVR",
            Category = "Wireless PCVR",
            Summary = "Free open-source wireless streaming from PC to Quest (SteamVR).",
            Url = "https://github.com/alvr-org/ALVR/releases/latest"
        },
        new VrToolLink
        {
            Name = "Steam Link (Quest)",
            Category = "Wireless PCVR",
            Summary = "Valve’s free wireless streaming path — use SteamVR OpenXR and Steam Link video settings.",
            Url = "https://store.steampowered.com/app/353380/Steam_Link/"
        },

        // Quest / sideloading
        new VrToolLink
        {
            Name = "SideQuest",
            Category = "Quest & sideloading",
            Summary = "Install APKs, browse sideloaded apps, and manage Developer Mode Quest utilities.",
            Url = "https://sidequestvr.com/"
        },
        new VrToolLink
        {
            Name = "Meta Quest Developer Hub",
            Category = "Quest & sideloading",
            Summary = "Official Meta tool for Device Manager, casting, and developer workflows.",
            Url = "https://developers.meta.com/horizon/downloads/package/oculus-developer-hub/"
        },
        new VrToolLink
        {
            Name = "Quest Games Optimizer",
            Category = "Quest & sideloading",
            Summary = "Community tool to tune per-game resolution / refresh on standalone Quest titles.",
            Url = "https://sidequestvr.com/app/21783/quest-games-optimizer"
        },

        // Tracking & calibration
        new VrToolLink
        {
            Name = "Space Calibrator",
            Category = "Tracking & calibration",
            Summary = "Align mixed tracking spaces (base stations + Quest / other devices) in SteamVR.",
            Url = "https://store.steampowered.com/app/1306720/Space_Calibrator/"
        },
        new VrToolLink
        {
            Name = "SlimeVR",
            Category = "Tracking & calibration",
            Summary = "DIY / IMU full-body tracking that feeds SteamVR trackers.",
            Url = "https://slimevr.dev/"
        },
        new VrToolLink
        {
            Name = "Amethyst",
            Category = "Tracking & calibration",
            Summary = "Body-tracking frontend that can drive SteamVR trackers from various backends.",
            Url = "https://docs.amethyst.dev/"
        },

        // Essentials
        new VrToolLink
        {
            Name = "SteamVR",
            Category = "Essentials",
            Summary = "Valve’s PCVR runtime — required for most Steam VR titles and SteamVR OpenXR.",
            Url = "https://store.steampowered.com/app/250820/SteamVR/"
        },
        new VrToolLink
        {
            Name = "Meta Quest Link (PC app)",
            Category = "Essentials",
            Summary = "Official Meta PC software for Quest Link / Air Link and the Oculus Debug Tool.",
            Url = "https://www.meta.com/quest/setup/"
        },
        new VrToolLink
        {
            Name = "OpenComposite",
            Category = "Essentials",
            Summary = "Translate OpenVR games to OpenXR — useful when a title expects SteamVR but you want OpenXR.",
            Url = "https://gitlab.com/znixian/OpenOVR/-/releases"
        }
    ];

    public static IEnumerable<IGrouping<string, VrToolLink>> ByCategory() =>
        All.GroupBy(tool => tool.Category);
}
