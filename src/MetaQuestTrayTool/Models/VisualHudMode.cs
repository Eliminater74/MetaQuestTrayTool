namespace MetaQuestTrayTool.Models;

/// <summary>
/// Persisted application choices. Use VisualHudMapping; these are not Meta CLI mode numbers.
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
