namespace MetaQuestTrayTool.Models;

public enum AudioDeviceKind
{
    Playback,
    Recording
}

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public AudioDeviceKind Kind { get; init; }
    public bool IsDefaultMultimedia { get; init; }
    public bool IsDefaultCommunications { get; init; }

    public override string ToString() => Name;
}
