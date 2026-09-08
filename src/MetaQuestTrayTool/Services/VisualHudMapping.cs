using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>Saved app enum values are not the installed Meta CLI's numeric modes.</summary>
public static class VisualHudMapping
{
    public static int? ToCliMode(VisualHudMode mode) => mode switch
    {
        VisualHudMode.None => 0,
        VisualHudMode.Performance or VisualHudMode.PerformanceHeadroom => 1,
        VisualHudMode.AppRenderTiming => 3,
        VisualHudMode.CompositorTiming => 4,
        VisualHudMode.AsynchronousSpacewarp => 6,
        VisualHudMode.Version => 5,
        // Version retains its legacy mapping; current ODT does not expose its label.
        _ => null
    };
}
