namespace MetaQuestTrayTool.Models;

/// <summary>App behaviour settings from the old OTT "Tray Tool" tab.</summary>
public sealed class TrayToolSettings
{
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeOnClose { get; set; } = true;
    public bool HideFromAltTab { get; set; }
    public bool CheckForUpdatesOnStart { get; set; } = true;
    public bool EnableHotKeys { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.Black;
}
