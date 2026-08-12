namespace MetaQuestTrayTool.Models;

public sealed class OpenXrSettings
{
    public OpenXrRuntimeKind PreferredRuntime { get; set; } = OpenXrRuntimeKind.Meta;
    public bool ApplyOnStart { get; set; }

    /// <summary>
    /// When a Steam Link / SteamVR session is detected without Meta Link, switch ActiveRuntime to SteamVR once per session,
    /// then restore <see cref="PreferredRuntime"/> when that session ends.
    /// </summary>
    public bool PreferSteamVrDuringSteamLink { get; set; } = true;
}
