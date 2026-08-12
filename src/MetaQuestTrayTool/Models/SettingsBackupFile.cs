namespace MetaQuestTrayTool.Models;

public sealed class SettingsBackupFile
{
    public string App { get; set; } = "Meta Quest Tray Tool";
    public string Version { get; set; } = "1.0.0";
    public DateTimeOffset ExportedAt { get; set; }
    public AppSettings Settings { get; set; } = new();
}
