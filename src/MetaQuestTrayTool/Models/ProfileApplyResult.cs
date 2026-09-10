namespace MetaQuestTrayTool.Models;

public enum ProfileApplyStatus { Success, PartialSuccess, Failed, NoChanges }
public enum ProfileStepStatus { Succeeded, Failed, Skipped }
public sealed record ProfileStepResult(string Name, ProfileStepStatus Status, string Summary);

public sealed record ProfileApplyResult(IReadOnlyList<ProfileStepResult> Steps)
{
    public ProfileApplyStatus Status
    {
        get
        {
            if (Steps.Count == 0 || Steps.All(s => s.Status == ProfileStepStatus.Skipped))
            {
                return ProfileApplyStatus.NoChanges;
            }

            if (Steps.All(s => s.Status == ProfileStepStatus.Succeeded))
            {
                return ProfileApplyStatus.Success;
            }

            return Steps.Any(s => s.Status == ProfileStepStatus.Succeeded)
                ? ProfileApplyStatus.PartialSuccess
                : ProfileApplyStatus.Failed;
        }
    }
    public string Title => Status switch
    {
        ProfileApplyStatus.Success => "Profile applied",
        ProfileApplyStatus.PartialSuccess => "Profile applied with warnings",
        ProfileApplyStatus.NoChanges => "Profile unchanged",
        _ => "Profile apply failed"
    };
    public string Summary => Title + ". " + string.Join(" ", Steps.Select(s => $"{s.Name}: {s.Summary}"));
    public override string ToString() => Summary;
}
