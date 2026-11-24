namespace MetaQuestTrayTool.Models;

/// <summary>
/// Provider preferences stored on a game profile. A matching, already-running provider can apply an opted-in GPU level 7.
/// The profile does not launch or switch a streaming stack, and it does not write refresh or session files.
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
