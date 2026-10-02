namespace MetaQuestTrayTool.Models;

/// <summary>App behaviour settings from the old OTT "Tray Tool" tab.</summary>
public sealed class TrayToolSettings
{
    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// When true, startup opens or activates Meta Horizon Link once.
    /// False by default so an upgrade does not launch the Meta client.
    /// </summary>
    public bool OpenMetaLinkOnStart { get; set; }
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
