using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class MetaRuntimeCompatibilityTests
{
    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, true, false, false)]
    public void OnlySuccessfulRequiredChecksPromoteBaseline(bool remember, bool requested, bool succeeded, bool warning, bool expected)
    {
        var validate = MetaRuntimeCompatibilityService.CanAutomaticallyValidate(remember, requested, succeeded,
            [new(warning ? MetaCompatibilityLevel.Warn : MetaCompatibilityLevel.Ok, "Probe", "Result")]);
        var state = MetaRuntimeCompatibilityService.BuildRememberedComponent("82", "runtime.exe", null, null, "81", "runtime.exe", "81", "runtime.exe", validate);
        Assert.Equal(expected ? "82" : "81", state.ValidatedVersion);
        Assert.Equal("82", state.DetectedVersion);
    }

    [Fact]
    public void FailedFirstCheckDoesNotCreateValidatedOrLegacyBaseline()
    {
        var state = MetaRuntimeCompatibilityService.BuildRememberedComponent("82", "runtime.exe", null, null, null, null, null, null, false);
        Assert.Equal("82", state.DetectedVersion);
        Assert.Null(state.ValidatedVersion);
        Assert.Null(state.LegacySeenVersion);
    }

    [Fact]
    public void FirstObservedVersionRequiresValidation()
    {
        var finding = MetaRuntimeCompatibilityService.EvaluateObservedComponent(
            "Meta runtime",
            "77.0.0.123",
            @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            lastVersion: null,
            lastPath: null);

        Assert.Equal(MetaCompatibilityLevel.Info, finding.Level);
        Assert.Contains("validation pending", finding.Title);
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
    public void FullCompatibilityCheckCanValidateChangedVersion()
    {
        var finding = MetaRuntimeCompatibilityService.EvaluateObservedComponent(
            "Oculus Debug Tool",
            "78.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-diagnostics\OculusDebugToolCLI.exe",
            "77.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-diagnostics\OculusDebugToolCLI.exe",
            acknowledgeCurrentVersion: true);

        Assert.Equal(MetaCompatibilityLevel.Info, finding.Level);
        Assert.Contains("validated", finding.Title);
        Assert.Contains("validated baseline", finding.Detail);
    }

    [Fact]
    public void StartupDetectionDoesNotReplaceExistingValidatedBaseline()
    {
        var remembered = MetaRuntimeCompatibilityService.BuildRememberedComponent(
            "82.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            detectedVersion: "81.0.0.1",
            detectedPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            validatedVersion: "81.0.0.1",
            validatedPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            legacySeenVersion: "81.0.0.1",
            legacySeenPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            validateCurrent: false);

        Assert.Equal("82.0.0.1", remembered.DetectedVersion);
        Assert.Equal("81.0.0.1", remembered.ValidatedVersion);
        Assert.Equal("81.0.0.1", remembered.LegacySeenVersion);
    }

    [Fact]
    public void FullCompatibilityCheckPromotesDetectedVersionToValidatedBaseline()
    {
        var remembered = MetaRuntimeCompatibilityService.BuildRememberedComponent(
            "82.0.0.1",
            @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            detectedVersion: "81.0.0.1",
            detectedPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            validatedVersion: "81.0.0.1",
            validatedPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            legacySeenVersion: "81.0.0.1",
            legacySeenPath: @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe",
            validateCurrent: true);

        Assert.Equal("82.0.0.1", remembered.DetectedVersion);
        Assert.Equal("82.0.0.1", remembered.ValidatedVersion);
        Assert.Equal("82.0.0.1", remembered.LegacySeenVersion);
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
