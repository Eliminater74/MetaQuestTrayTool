namespace MetaQuestTrayTool.Models;

public sealed class LinkApplyResult
{
    public bool Succeeded { get; init; }
    public LinkSettings? Written { get; init; }
    public LinkSettings? Current { get; init; }
    public string Summary { get; init; } = string.Empty;
}
