namespace MetaQuestTrayTool.Models;

public sealed class AudioSwitchSettings
{
    public bool AutoSwitchEnabled { get; set; }
    public string? VrPlaybackDeviceId { get; set; }
    public string? VrRecordingDeviceId { get; set; }
    public string? FallbackPlaybackDeviceId { get; set; }
    public string? FallbackRecordingDeviceId { get; set; }
    public bool AlsoSetCommunicationsRole { get; set; } = true;
}
