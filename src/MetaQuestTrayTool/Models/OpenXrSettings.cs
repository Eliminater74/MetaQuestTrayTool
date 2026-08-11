namespace MetaQuestTrayTool.Models;

public sealed class OpenXrSettings
{
    public OpenXrRuntimeKind PreferredRuntime { get; set; } = OpenXrRuntimeKind.Meta;
    public bool ApplyOnStart { get; set; }
}
