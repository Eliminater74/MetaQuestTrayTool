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
}
