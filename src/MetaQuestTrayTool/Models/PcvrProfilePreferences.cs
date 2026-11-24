namespace MetaQuestTrayTool.Models;

/// <summary>
/// Optional provider notes stored on a game profile. They never launch or switch a streaming stack.
/// </summary>
public sealed class PcvrProfilePreferences
{
    public string? PreferredProvider { get; set; }
    public bool AutoSwitchProvider { get; set; }
    public bool? ExperimentalGpuMaximum { get; set; }
    public int? ExperimentalRefreshHz { get; set; }
    public string? AlvrProfile { get; set; }
    public string? PyrowaveProfile { get; set; }
    public bool CollectDiagnostics { get; set; }

    public PcvrProfilePreferences Clone() => new()
    {
        PreferredProvider = PreferredProvider,
        AutoSwitchProvider = AutoSwitchProvider,
        ExperimentalGpuMaximum = ExperimentalGpuMaximum,
        ExperimentalRefreshHz = ExperimentalRefreshHz,
        AlvrProfile = AlvrProfile,
        PyrowaveProfile = PyrowaveProfile,
        CollectDiagnostics = CollectDiagnostics
    };
}
