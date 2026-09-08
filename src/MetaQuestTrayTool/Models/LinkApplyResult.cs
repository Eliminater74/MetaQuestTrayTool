namespace MetaQuestTrayTool.Models;

public sealed class LinkApplyResult
{
    /// <summary>True only when registry writes/deletions match read-back; does not prove runtime application.</summary>
    public bool Succeeded { get; init; }
    public LinkSettings? Written { get; init; }
    public LinkSettings? Current { get; init; }
    public IReadOnlyList<string> Mismatches { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
}
