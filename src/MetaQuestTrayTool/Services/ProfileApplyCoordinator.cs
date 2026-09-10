using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>Collects every requested operation, including non-throwing failures.</summary>
public sealed class ProfileApplyCoordinator
{
    private readonly List<ProfileStepResult> _steps = [];
    public void Run(string name, Func<ProfileStepResult> apply)
    {
        try { _steps.Add(apply()); }
        catch (Exception ex) { _steps.Add(new(name, ProfileStepStatus.Failed, ex.Message)); }
    }
    public ProfileApplyResult Result => new(_steps.ToArray());
}
