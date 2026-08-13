namespace MetaQuestTrayTool.Models;

public sealed class VoiceSettings
{
    public bool Enabled { get; set; }

    /// <summary>When true, voice listens only after the push-to-talk shortcut (recommended in VR).</summary>
    public bool PushToTalkOnly { get; set; } = true;

    public bool AudioConfirmation { get; set; } = true;

    public HotKeyModifiers PushToTalkModifiers { get; set; } = HotKeyModifiers.Control | HotKeyModifiers.Shift;

    public string PushToTalkKey { get; set; } = "V";

    /// <summary>
    /// Preferred Windows recording endpoint for speech. Empty = leave Windows default alone.
    /// System.Speech uses the default capture device, so we temporarily switch when listening.
    /// </summary>
    public string? PreferredRecordingDeviceId { get; set; }

    /// <summary>
    /// Minimum recognition confidence (0–1). Higher reduces false triggers in always-on mode.
    /// </summary>
    public float MinConfidence { get; set; } = 0.60f;

    public List<VoiceCustomPhrase> CustomPhrases { get; set; } = [];

    public VoiceSettings Clone() => new()
    {
        Enabled = Enabled,
        PushToTalkOnly = PushToTalkOnly,
        AudioConfirmation = AudioConfirmation,
        PushToTalkModifiers = PushToTalkModifiers,
        PushToTalkKey = PushToTalkKey,
        PreferredRecordingDeviceId = PreferredRecordingDeviceId,
        MinConfidence = MinConfidence,
        CustomPhrases = CustomPhrases
            .Select(phrase => new VoiceCustomPhrase { Phrase = phrase.Phrase, Action = phrase.Action })
            .ToList()
    };

    public HotKeyBinding ToPushToTalkBinding() => new()
    {
        Id = VoicePhraseCatalog.PushToTalkHotKeyId,
        Action = HotKeyAction.VoicePushToTalk,
        Modifiers = PushToTalkModifiers,
        Key = PushToTalkKey
    };

    public IEnumerable<VoiceCustomPhrase> NormalizedCustomPhrases() =>
        (CustomPhrases ?? [])
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase.Phrase)
                             && phrase.Action != HotKeyAction.VoicePushToTalk)
            .Select(phrase => new VoiceCustomPhrase
            {
                Phrase = phrase.Phrase.Trim(),
                Action = phrase.Action
            });
}
