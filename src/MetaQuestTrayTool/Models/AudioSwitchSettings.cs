namespace MetaQuestTrayTool.Models;

public enum AudioSwitchTrigger
{
    /// <summary>
    /// Switch when the VR headset audio endpoint becomes active (Air Link / USB Link),
    /// and restore when that endpoint disappears.
    /// </summary>
    LinkAudioDevice = 0,

    /// <summary>Legacy behaviour: follow OVRService running/stopped.</summary>
    OculusService = 1
}

public sealed class AudioSwitchSettings
{
    public bool AutoSwitchEnabled { get; set; } = true;
    public AudioSwitchTrigger Trigger { get; set; } = AudioSwitchTrigger.LinkAudioDevice;
    public string? VrPlaybackDeviceId { get; set; }
    public string? VrRecordingDeviceId { get; set; }
    public string? FallbackPlaybackDeviceId { get; set; }
    public string? FallbackRecordingDeviceId { get; set; }
    public string? VrCommunicationsPlaybackDeviceId { get; set; }
    public string? VrCommunicationsRecordingDeviceId { get; set; }
    public string? FallbackCommunicationsPlaybackDeviceId { get; set; }
    public string? FallbackCommunicationsRecordingDeviceId { get; set; }
    public bool AlsoSetCommunicationsRole { get; set; } = true;
    public bool CaptureFallbackOnEachLinkSession { get; set; } = true;
}
