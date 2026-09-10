using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class MetaRuntimeCompatibilityTests
{
    [Fact]
    public void FirstObservedVersionRecordsBaselineWithoutWarning()
    {
        var finding = MetaRuntimeCompatibilityService.EvaluateObservedComponent(
            "Meta runtime",
            "77.0.0.123",
            @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            lastVersion: null,
            lastPath: null);

        Assert.Equal(MetaCompatibilityLevel.Info, finding.Level);
        Assert.Contains("baseline recorded", finding.Title);
    }

    [Fact]
    public void ChangedVersionWarnsThatMappingsNeedRetest()
    {
        var finding = MetaRuntimeCompatibilityService.EvaluateObservedComponent(
            "Oculus Debug Tool",
            "78.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-diagnostics\OculusDebugToolCLI.exe",
            "77.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-diagnostics\OculusDebugToolCLI.exe");

        Assert.Equal(MetaCompatibilityLevel.Warn, finding.Level);
        Assert.Contains("changed", finding.Title);
        Assert.Contains("Re-test", finding.Detail);
    }

    [Fact]
    public void MissingRuntimeExecutableIsWarning()
    {
        var finding = MetaRuntimeCompatibilityService.EvaluateObservedComponent(
            "Meta runtime",
            currentVersion: null,
            currentPath: null,
            "77.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe");

        Assert.Equal(MetaCompatibilityLevel.Warn, finding.Level);
    }
}
