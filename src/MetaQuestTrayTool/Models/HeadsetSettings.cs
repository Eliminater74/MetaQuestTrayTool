namespace MetaQuestTrayTool.Models;

public enum HeadsetCpuGpuLevel
{
    AppDefault,
    Level2,
    Level4
}

public enum HeadsetTexturePreset
{
    DeviceDefault,
    Quest1,
    Quest2,
    Quest3,
    Square512,
    Square768,
    Square1024,
    Square1280,
    Square1536,
    Square2048,
    Square2560,
    Square3072
}

public enum HeadsetRefreshRate
{
    DeviceDefault,
    Hz60,
    Hz72,
    Hz80,
    Hz90,
    Hz120
}

public enum HeadsetFfrLevel
{
    DeviceDefault,
    Off,
    Low,
    Medium,
    High,
    HighTop
}

public enum HeadsetChromaMode
{
    AppSelected,
    On,
    Off
}

public enum HeadsetCaptureSize
{
    DeviceDefault,
    Size640x480,
    Size1280x720,
    Size1920x1080,
    Size1024x1024,
    Size1600x1600
}

public enum HeadsetCaptureFps
{
    DeviceDefault,
    Fps24,
    Fps30,
    Fps60
}

public enum HeadsetCaptureBitrate
{
    DeviceDefault,
    Mbps5,
    Mbps10,
    Mbps15,
    Mbps20
}

/// <summary>Standalone Quest tweaks applied over ADB (same properties SideQuest uses).</summary>
public sealed class HeadsetSettings
{
    public bool ApplyWhenHeadsetConnects { get; set; } = true;
    public HeadsetCpuGpuLevel CpuGpuLevel { get; set; } = HeadsetCpuGpuLevel.AppDefault;
    public HeadsetTexturePreset TextureSize { get; set; } = HeadsetTexturePreset.DeviceDefault;
    public HeadsetRefreshRate RefreshRate { get; set; } = HeadsetRefreshRate.DeviceDefault;
    public HeadsetFfrLevel Ffr { get; set; } = HeadsetFfrLevel.DeviceDefault;
    public HeadsetChromaMode ChromaticAberration { get; set; } = HeadsetChromaMode.AppSelected;
    public HeadsetCaptureSize CaptureSize { get; set; } = HeadsetCaptureSize.DeviceDefault;
    public HeadsetCaptureFps CaptureFps { get; set; } = HeadsetCaptureFps.DeviceDefault;
    public HeadsetCaptureBitrate CaptureBitrate { get; set; } = HeadsetCaptureBitrate.DeviceDefault;
    public bool StereoCapture { get; set; }
    public bool FullRateCapture { get; set; }
    public bool RequireTrustedHeadset { get; set; } = true;
    public string? TrustedSerial { get; set; }
    public string? TrustedModel { get; set; }

    /// <summary>Last wireless ADB host (LAN IPv4). Empty = not configured.</summary>
    public string? WirelessHost { get; set; }

    /// <summary>Wireless ADB port. Classic tcpip mode is 5555; Wireless debugging uses a dynamic port.</summary>
    public int WirelessPort { get; set; } = 5555;

    /// <summary>When no USB headset is present, periodically try <c>adb connect</c> to the saved host:port.</summary>
    public bool WirelessAutoReconnect { get; set; }

    /// <summary>
    /// Drop wireless ADB sessions that are not a VR headset (phones/tablets Adb mDNS-discovers on the LAN).
    /// USB devices are left listed; commands still never run on them.
    /// </summary>
    public bool HeadsetOnlyWirelessAdb { get; set; } = true;

    public string? WirelessEndpoint
    {
        get
        {
            var host = (WirelessHost ?? string.Empty).Trim();
            if (host.Length == 0 || WirelessPort is < 1 or > 65535)
            {
                return null;
            }

            return $"{host}:{WirelessPort}";
        }
    }

    private List<string> _customAdbCommands = [];
    public List<string> CustomAdbCommands
    {
        get => _customAdbCommands;
        set => _customAdbCommands = value ?? [];
    }
}
