namespace MetaQuestTrayTool.Models;

public sealed class DebugToolApplyResult
{
    public bool CliFound { get; init; }
    public bool Started { get; init; }
    public int ExitCode { get; init; } = -1;
    public IReadOnlyList<string> Commands { get; init; } = [];
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool LooksRejected { get; init; }
    public string Summary { get; init; } = string.Empty;
}
