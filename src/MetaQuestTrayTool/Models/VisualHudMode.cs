namespace MetaQuestTrayTool.Models;

/// <summary>
/// Oculus Debug Tool Performance HUD. Maps to `perfhud set-mode` / `perfhud reset`.
/// </summary>
public enum VisualHudMode
{
    None = 0,
    Performance = 1,
    AppRenderTiming = 2,
    CompositorTiming = 3,
    PerformanceHeadroom = 4,
    Version = 5,
    AsynchronousSpacewarp = 6
}
