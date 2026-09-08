using System.Text.Json;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class GameControlTests
{
    [Theory]
    [InlineData(VisualHudMode.Performance, 1)]
    [InlineData(VisualHudMode.PerformanceHeadroom, 1)]
    [InlineData(VisualHudMode.AppRenderTiming, 3)]
    [InlineData(VisualHudMode.CompositorTiming, 4)]
    [InlineData(VisualHudMode.AsynchronousSpacewarp, 6)]
    public void HudUsesObservedMetaModeRatherThanSavedEnumOrdinal(VisualHudMode mode, int cliMode)
    {
        var service = new OculusDebugToolService(new OculusRuntimeService());
        Assert.Contains($"perfhud set-mode {cliMode}", service.BuildCommands(new GameSettings { VisualHud = mode }));
    }

    [Fact]
    public void UnknownHudIsRejectedBeforeAnyRuntimeCommands()
    {
        var service = new OculusDebugToolService(new OculusRuntimeService());
        var result = service.Apply(new GameSettings { VisualHud = (VisualHudMode)999 });
        Assert.False(result.Succeeded);
        Assert.False(result.Started);
        Assert.Empty(result.Commands);
        Assert.Contains("unknown", result.Summary);
        Assert.Equal(5, VisualHudMapping.ToCliMode(VisualHudMode.Version));
        Assert.Equal(5, (int)VisualHudMode.Version);
        Assert.Equal(6, (int)VisualHudMode.AsynchronousSpacewarp);
    }
    [Fact]
    public void SeparateFovAxesSurviveJsonRoundTripWithoutLegacyAlias()
    {
        var settings = new GameSettings { FovMultiplierHorizontal = 0.8, FovMultiplierVertical = 0.9 };
        var json = JsonSerializer.Serialize(settings);
        Assert.DoesNotContain("\"FovMultiplier\":", json);
        var restored = JsonSerializer.Deserialize<GameSettings>(json)!;
        Assert.Equal(0.8, restored.FovMultiplierHorizontal);
        Assert.Equal(0.9, restored.FovMultiplierVertical);
    }

    [Theory]
    [InlineData("{\"FovMultiplier\":0.7}", 0.7, 0.7)]
    [InlineData("{\"FovMultiplierHorizontal\":0.8,\"FovMultiplierVertical\":0.9,\"FovMultiplier\":0.7}", 0.8, 0.9)]
    [InlineData("{\"FovMultiplier\":0.7,\"FovMultiplierHorizontal\":1,\"FovMultiplierVertical\":0.9}", 1, 0.9)]
    [InlineData("{\"FovMultiplierHorizontal\":0.8,\"FovMultiplier\":0.7}", 0.8, 0.7)]
    public void LegacyFovFillsOnlyMissingAxesRegardlessOfPropertyOrder(string json, double h, double v)
    {
        var settings = JsonSerializer.Deserialize<GameSettings>(json)!;
        Assert.Equal(h, settings.FovMultiplierHorizontal);
        Assert.Equal(v, settings.FovMultiplierVertical);
    }

    [Fact]
    public void DefaultFovSendsResetAfterReducedProfile()
    {
        var service = new OculusDebugToolService(new OculusRuntimeService());
        Assert.Contains("service set-client-fov-tan-angle-multiplier 0.8 0.9",
            service.BuildCommands(new GameSettings { FovMultiplierHorizontal = 0.8, FovMultiplierVertical = 0.9 }));
        Assert.Contains("service set-client-fov-tan-angle-multiplier 1 1", service.BuildCommands(new GameSettings()));
    }

    [Theory]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(0.49, false)]
    [InlineData(1.51, false)]
    [InlineData(0.5, true)]
    [InlineData(1.5, true)]
    public void FovRejectsNonFiniteAndOutOfRangeInput(double value, bool expected) =>
        Assert.Equal(expected, GameSettings.IsValidFov(value));

    [Fact]
    public void GameBooleanCommandsMatchInstalledCliHelpAndHudResetIsExplicit()
    {
        var commands = new OculusDebugToolService(new OculusRuntimeService()).BuildCommands(new GameSettings
        {
            ForceMipMapOnLayers = true, OffsetMipMapOnLayers = 1, AdaptiveGpuScaling = false,
            UseFovStencil = false, SuperSampling = 0, VisualHud = VisualHudMode.None
        });
        Assert.Contains("service set-offset-mip-bias-on-all-layers true", commands);
        Assert.Contains("service set-force-mip-gen-on-all-layers true", commands);
        Assert.Contains("service enable-adaptive-gpu-perf-scale false", commands);
        Assert.Contains("service set-use-fov-stencil false", commands);
        Assert.Contains("service set-pixels-per-display-pixel-override 0", commands);
        Assert.Contains("perfhud reset", commands);
    }
}
