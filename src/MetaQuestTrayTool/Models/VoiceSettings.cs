namespace MetaQuestTrayTool.Models;

public sealed class VoiceSettings
{
    public bool Enabled { get; set; }

    /// <summary>When true, voice listens only after the push-to-talk shortcut (recommended in VR).</summary>
    public bool PushToTalkOnly { get; set; } = true;

    public bool AudioConfirmation { get; set; } = true;

    public HotKeyModifiers PushToTalkModifiers { get; set; } = HotKeyModifiers.Control | HotKeyModifiers.Shift;

    public string PushToTalkKey { get; set; } = "V";

    public VoiceSettings Clone() => new()
    {
        Enabled = Enabled,
        PushToTalkOnly = PushToTalkOnly,
        AudioConfirmation = AudioConfirmation,
        PushToTalkModifiers = PushToTalkModifiers,
        PushToTalkKey = PushToTalkKey
    };

    public HotKeyBinding ToPushToTalkBinding() => new()
    {
        Id = VoicePhraseCatalog.PushToTalkHotKeyId,
        Action = HotKeyAction.VoicePushToTalk,
        Modifiers = PushToTalkModifiers,
        Key = PushToTalkKey
    };
}
