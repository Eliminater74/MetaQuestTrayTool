namespace MetaQuestTrayTool.Models;

public enum HeadsetPerformanceLevel { AppDefault = -1, Level0 = 0, Level1, Level2, Level3, Level4 }

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

public enum HeadsetFoveationMode { AppDefault, Dynamic, Fixed }

public enum HeadsetExperimentalOverride { AppDefault, ForceOff, ForceOn }

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
    Mbps20,
    Mbps30,
    Mbps40
}

/// <summary>Standalone Quest tweaks applied over ADB (same properties SideQuest uses).</summary>
public sealed class HeadsetSettings
{
    public bool ApplyWhenHeadsetConnects { get; set; } = true;
    public HeadsetCpuGpuLevel CpuGpuLevel { get; set; } = HeadsetCpuGpuLevel.AppDefault;
    // Null means an older settings file: preserve its combined CPU/GPU choice.
    public HeadsetPerformanceLevel? CpuLevel { get; set; }
    public HeadsetPerformanceLevel? GpuLevel { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public HeadsetPerformanceLevel EffectiveCpuLevel => CpuLevel ?? LegacyPerformanceLevel;
    [System.Text.Json.Serialization.JsonIgnore]
    public HeadsetPerformanceLevel EffectiveGpuLevel => GpuLevel ?? LegacyPerformanceLevel;
    private HeadsetPerformanceLevel LegacyPerformanceLevel => CpuGpuLevel switch
    {
        HeadsetCpuGpuLevel.Level2 => HeadsetPerformanceLevel.Level2,
        HeadsetCpuGpuLevel.Level4 => HeadsetPerformanceLevel.Level4,
        _ => HeadsetPerformanceLevel.AppDefault
    };
    public HeadsetTexturePreset TextureSize { get; set; } = HeadsetTexturePreset.DeviceDefault;
    public HeadsetRefreshRate RefreshRate { get; set; } = HeadsetRefreshRate.DeviceDefault;
    public HeadsetFoveationMode? FoveationMode { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public HeadsetFoveationMode EffectiveFoveationMode => FoveationMode
        ?? (Ffr == HeadsetFfrLevel.DeviceDefault ? HeadsetFoveationMode.AppDefault : HeadsetFoveationMode.Fixed);
    public HeadsetFfrLevel Ffr { get; set; } = HeadsetFfrLevel.DeviceDefault;
    public HeadsetChromaMode ChromaticAberration { get; set; } = HeadsetChromaMode.AppSelected;
    public HeadsetCaptureSize CaptureSize { get; set; } = HeadsetCaptureSize.DeviceDefault;
    public HeadsetCaptureFps CaptureFps { get; set; } = HeadsetCaptureFps.DeviceDefault;
    public HeadsetCaptureBitrate CaptureBitrate { get; set; } = HeadsetCaptureBitrate.DeviceDefault;
    public bool StereoCapture { get; set; }
    public bool FullRateCapture { get; set; }
    public HeadsetExperimentalOverride LocalDimming { get; set; } = HeadsetExperimentalOverride.AppDefault;
    public HeadsetExperimentalOverride SubsampledFoveation { get; set; } = HeadsetExperimentalOverride.AppDefault;
    public bool RequireTrustedHeadset { get; set; } = true;
    public string? TrustedSerial { get; set; }
    public string? TrustedModel { get; set; }

    /// <summary>Last wireless ADB host (LAN IPv4). Empty = not configured.</summary>
    public string? WirelessHost { get; set; }

    /// <summary>Wireless ADB port. Classic tcpip mode is 5555; Wireless debugging uses a dynamic port.</summary>
    public int WirelessPort { get; set; } = 5555;

    /// <summary>
    /// When a PCVR session is active and no ready Quest is listed, try <c>adb connect</c> to the saved host:port.
    /// This does not poll while the tray is idle.
    /// </summary>
    public bool WirelessAutoReconnect { get; set; }

    /// <summary>
    /// Retired. Older builds disconnected non-headset wireless ADB devices and defaulted this to true.
    /// It is forced off on load so a saved default cannot keep evicting phones, TVs, or emulators.
    /// Quest commands stay targeted through headset classification instead.
    /// </summary>
    public bool HeadsetOnlyWirelessAdb { get; set; }

    /// <summary>Set once exclusive wireless disconnect has been cleared. Kept so old files cannot opt back in silently.</summary>
    public bool ExclusiveWirelessSweepRetired { get; set; }

    /// <summary>
    /// Suppresses automatic PCVR-session ADB. Manual actions still run when requested.
    /// Resume allows the next live PCVR session to use ADB; it does not poll while idle.
    /// </summary>
    public bool AdbWatcherPaused { get; set; }

    /// <summary>
    /// UTC end of a timed pause. Null means pause until you Resume (while <see cref="AdbWatcherPaused"/> is true).
    /// </summary>
    public DateTime? AdbWatcherPausedUntilUtc { get; set; }

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

    /// <summary>
    /// Clears exclusive non-headset disconnect. Returns true when a saved true value was turned off.
    /// </summary>
    public static bool RetireExclusiveWirelessSweep(HeadsetSettings settings)
    {
        var hadExclusive = settings.HeadsetOnlyWirelessAdb;
        settings.HeadsetOnlyWirelessAdb = false;
        settings.ExclusiveWirelessSweepRetired = true;
        return hadExclusive;
    }

    private List<string> _customAdbCommands = [];
    public List<string> CustomAdbCommands
    {
        get => _customAdbCommands;
        set => _customAdbCommands = value ?? [];
    }
}
