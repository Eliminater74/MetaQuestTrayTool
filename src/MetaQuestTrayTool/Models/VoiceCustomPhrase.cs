namespace MetaQuestTrayTool.Models;

/// <summary>User-defined voice phrase mapped to a tray action.</summary>
public sealed class VoiceCustomPhrase
{
    public string Phrase { get; set; } = string.Empty;
    public HotKeyAction Action { get; set; } = HotKeyAction.ApplyGlobal;
}
