using MetaQuestTrayTool.Overlay;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class OverlayCompanionTests
{
    [Fact]
    public void TrayAssemblyDoesNotReferenceTheOverlayOrOpenVr()
    {
        var names = typeof(OverlayProcessHost).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToList();
        Assert.DoesNotContain(names, name => name is "MetaQuestTrayTool.Overlay" or "Valve.VR" or "openvr_api");
    }

    [Fact]
    public void CompanionStartInfoDoesNotPutTheTokenOnTheCommandLine()
    {
        var info = OverlayProcessHost.CreateStartInfo(@"C:\overlay\MetaQuestTrayTool.Overlay.exe");
        Assert.True(string.IsNullOrEmpty(info.Arguments));
        Assert.True(info.RedirectStandardInput);
        Assert.False(info.UseShellExecute);
        var line = OverlayInteraction.AuthLine("abc123", "snapshot");
        Assert.Equal("auth abc123 snapshot", line);
        Assert.DoesNotContain(line, info.Arguments ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardClickCyclesModesAndStopIsSeparate()
    {
        Assert.Equal(OverlayDisplayMode.Detail, OverlayInteraction.NextMode(0.1f, 0.8f, OverlayDisplayMode.Compact));
        Assert.Equal(OverlayDisplayMode.Hidden, OverlayInteraction.NextMode(0.1f, 0.9f, OverlayDisplayMode.Detail));
        Assert.Equal(OverlayDisplayMode.Compact, OverlayInteraction.NextMode(0.2f, 0.8f, OverlayDisplayMode.Hidden));
        Assert.Equal(OverlayDisplayMode.Compact, OverlayInteraction.NextMode(0.8f, 0.9f, OverlayDisplayMode.Compact));
        Assert.True(OverlayInteraction.IsBenchmarkStop(0.8f, 0.9f));
        Assert.False(OverlayInteraction.IsBenchmarkStop(0.2f, 0.2f));
    }

    [Fact]
    public void HiddenSurfaceIsBlankAndDetailDiffersFromCompact()
    {
        var hidden = OverlaySurface.Render("FPS 90", OverlayDisplayMode.Hidden);
        var compact = OverlaySurface.Render("FPS 90", OverlayDisplayMode.Compact);
        var detail = OverlaySurface.Render("FPS 90" + Environment.NewLine + "provider ALVR", OverlayDisplayMode.Detail);
        Assert.Equal(OverlaySurface.Width * OverlaySurface.Height * 4, hidden.Length);
        Assert.All(hidden, pixel => Assert.Equal(0, pixel));
        Assert.NotEqual(compact, detail);
    }
}
