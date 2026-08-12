namespace MetaQuestTrayTool.Models;

/// <summary>App behaviour settings from the old OTT "Tray Tool" tab.</summary>
public sealed class TrayToolSettings
{
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeOnClose { get; set; } = true;
    public bool HideFromAltTab { get; set; }
    public bool CheckForUpdatesOnStart { get; set; } = true;

    /// <summary>Periodic background check while the tray is running.</summary>
    public UpdateCheckInterval AutoUpdateCheckInterval { get; set; } = UpdateCheckInterval.Weekly;

    /// <summary>UTC time of the last successful GitHub release check.</summary>
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public bool EnableHotKeys { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.Black;
}
