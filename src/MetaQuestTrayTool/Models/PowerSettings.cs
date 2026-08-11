namespace MetaQuestTrayTool.Models;

public sealed class PowerPlanInfo
{
    public required Guid Guid { get; init; }
    public required string Name { get; init; }
    public bool IsActive { get; init; }

    public override string ToString() => IsActive ? $"{Name} (active)" : Name;
}

public sealed class PowerSettings
{
    public bool AutoSwitchEnabled { get; set; }
    public string? VrPlanGuid { get; set; }
    public string? FallbackPlanGuid { get; set; }
    public bool DisableUsbSelectiveSuspendWhileRunning { get; set; }
    public bool RestartServiceAfterSleep { get; set; }
}
