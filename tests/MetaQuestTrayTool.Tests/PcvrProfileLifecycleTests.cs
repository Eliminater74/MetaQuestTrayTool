using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PcvrProfileLifecycleTests
{
    [Fact]
    public void ProductionLifecycleAppliesGpuOnlyWhenTheProviderAndHeadsetMatch()
    {
        var actions = new RecordingActions();
        var lifecycle = new PcvrProfileLifecycle();
        var coordinator = new ProfileApplyCoordinator();
        coordinator.Run("ODT", () => new ProfileStepResult("ODT", ProfileStepStatus.Succeeded, "ODT applied"));
        coordinator.Run("PCVR", () => lifecycle.Apply(GpuPreference("Meta Quest Link"), Ready("Meta Quest Link"), actions));

        var pcvr = coordinator.Result.Steps.Single(step => step.Name == "PCVR");
        Assert.Equal(ProfileStepStatus.Succeeded, pcvr.Status);
        Assert.Equal(["7"], actions.GpuWrites);
        Assert.Contains("No streaming provider was launched", pcvr.Summary, StringComparison.Ordinal);
        Assert.Contains("universal hardware", pcvr.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ODT applied", coordinator.Result.Steps.Single(step => step.Name == "ODT").Summary, StringComparison.Ordinal);

        var restored = lifecycle.Restore(actions);
        Assert.Equal(["serial-1"], actions.Restores);
        Assert.Equal(ProfileStepStatus.Succeeded, restored.Status);
        var second = lifecycle.Restore(actions);
        Assert.Contains("nothing was restored", second.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(actions.Restores);
    }

    [Fact]
    public void UnsafePreferencesDoNotWriteOrLaunch()
    {
        var actions = new RecordingActions();
        var lifecycle = new PcvrProfileLifecycle();
        var blocked = lifecycle.Apply(new PcvrProfilePreferences
        {
            PreferredProvider = "Pyrowave",
            AutoSwitchProvider = true,
            ExperimentalGpuMaximum = true,
            ExperimentalRefreshHz = 207,
            PyrowaveProfile = "Quality 120 Hz"
        }, Ready("Meta Quest Link"), actions);

        Assert.Empty(actions.GpuWrites);
        Assert.Equal(ProfileStepStatus.Skipped, blocked.Status);
        Assert.Contains("ignored", blocked.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not match", blocked.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not written", blocked.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("were not applied", blocked.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No streaming provider was launched", blocked.Summary, StringComparison.Ordinal);

        var latched = lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR") with { ThermalReapplyBlocked = true }, actions);
        var missingTemperature = lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR") with { BatteryCelsius = null }, actions);
        var otherHeadset = lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR") with { HeadsetSerial = "other" }, actions);
        var refreshOnly = lifecycle.Apply(new PcvrProfilePreferences
        {
            PreferredProvider = "ALVR",
            ExperimentalRefreshHz = 144
        }, Ready("ALVR"), actions);
        Assert.Empty(actions.GpuWrites);
        Assert.Contains("blocked", latched.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not available", missingTemperature.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not confirmed", otherHeadset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not written", refreshOnly.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AStaleProfileOperationWritesNothing()
    {
        var actions = new RecordingActions();
        var lifecycle = new PcvrProfileLifecycle();
        lifecycle.BeforeCommit = generation =>
        {
            if (generation == 1)
            {
                lifecycle.Apply(new PcvrProfilePreferences { PreferredProvider = "ALVR" }, Ready("ALVR"), actions);
            }
        };

        var stale = lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR"), actions);
        Assert.Empty(actions.GpuWrites);
        Assert.Contains("wrote nothing", stale.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnExistingGpuLevel7IsNotRestoredWhenTheGameExits()
    {
        var actions = new RecordingActions { AlreadyApplied = true };
        var lifecycle = new PcvrProfileLifecycle();
        var applied = lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR"), actions);
        Assert.Equal(ProfileStepStatus.Succeeded, applied.Status);
        Assert.Empty(actions.GpuWrites);
        Assert.False(actions.OwnsRestore);
        Assert.Contains("already applied", applied.Summary, StringComparison.OrdinalIgnoreCase);
        var restored = lifecycle.Restore(actions);
        Assert.Empty(actions.Restores);
        Assert.Contains("nothing was restored", restored.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(PcvrProfileLifecycle.OwnsNewGpuOverride("7"));
        Assert.False(PcvrProfileLifecycle.OwnsNewGpuOverride(" 7 "));
        Assert.True(PcvrProfileLifecycle.OwnsNewGpuOverride("4"));
        Assert.True(PcvrProfileLifecycle.OwnsNewGpuOverride(null));
    }

    [Fact]
    public void RestoreFailureStaysFailedAndCanBeRetried()
    {
        var actions = new RecordingActions { FailRestore = true };
        var lifecycle = new PcvrProfileLifecycle();
        lifecycle.Apply(GpuPreference("ALVR"), Ready("ALVR"), actions);
        var failed = lifecycle.Restore(actions);
        Assert.Equal(ProfileStepStatus.Failed, failed.Status);
        actions.FailRestore = false;
        var retried = lifecycle.Restore(actions);
        Assert.Equal(ProfileStepStatus.Succeeded, retried.Status);
    }

    private static PcvrProfilePreferences GpuPreference(string provider) => new()
    {
        PreferredProvider = provider,
        ExperimentalGpuMaximum = true
    };

    private static PcvrProfileContext Ready(string provider) => new()
    {
        SessionActive = true,
        ActiveProvider = provider,
        TrustedSerial = "trusted",
        HeadsetSerial = "trusted",
        AdbSerial = "serial-1",
        Model = "Quest 3",
        Fingerprint = "fingerprint",
        HeadsetReady = true,
        ExperimentalGpuOptIn = true,
        BatteryCelsius = 40
    };

    private sealed class RecordingActions : IPcvrProfileActions
    {
        public List<string> GpuWrites { get; } = [];
        public List<string> Restores { get; } = [];
        public bool FailRestore { get; set; }

        public bool AlreadyApplied { get; init; }
        public bool OwnsRestore { get; private set; }

        public string ApplyExperimentalGpu(PcvrProfileContext context)
        {
            if (AlreadyApplied)
            {
                OwnsRestore = false;
                return "GPU level 7 was already applied.";
            }

            OwnsRestore = true;
            GpuWrites.Add("7");
            return "Verified GPU level 7. No streaming provider was launched.";
        }

        public string RestoreExperimentalGpu(string serial)
        {
            if (FailRestore)
            {
                throw new InvalidOperationException("restore failed");
            }

            Restores.Add(serial);
            return "Restored the profile GPU property.";
        }
    }
}
