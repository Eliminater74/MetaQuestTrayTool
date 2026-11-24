using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class ExperimentalGpuThermalGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RisingTemperatureRestoresOnceAndBlocksAnotherWatch()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        Assert.Equal(PropertyTransactionStatus.Verified, service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel"] = "7"
        }).Status);

        var temps = new Queue<double?>(new double?[] { 40, 46 });
        var reads = 0;
        var guard = Guard(serial =>
        {
            reads++;
            return Sample(temps.Dequeue());
        }, (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));

        var outcome = guard.Run("quest", "fp", serial =>
        {
            reads++;
            return Sample(temps.Dequeue());
        }, (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);

        Assert.True(outcome.Restored);
        Assert.Equal(PropertyTransactionStatus.Restored, outcome.RestorationStatus);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.Contains("not an established universal hardware safety limit", outcome.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(guard.AutomaticReapplyBlocked);
        Assert.Equal(2, reads);

        var blocked = guard.Run("quest", "fp", _ =>
        {
            reads++;
            return Sample(30);
        }, (_, _) => throw new InvalidOperationException("should not restore again"), () => Now, (_, _) => { }, CancellationToken.None);
        Assert.True(blocked.BlockedAutomaticReapply);
        Assert.Equal(2, reads);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disconnect")]
    [InlineData("stale")]
    [InlineData("severe")]
    public void ConservativeReadingsRestore(string kind)
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        var first = true;
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
        var outcome = guard.Run("quest", "fp", _ =>
        {
            if (first)
            {
                first = false;
                return Sample(36);
            }

            return kind switch
            {
                "missing" => Sample(null),
                "disconnect" => ThermalReading.Unreachable(Now, "IOException"),
                "stale" => new ThermalReading { HeadsetReachable = true, BatteryCelsius = 36, SampledUtc = Now.AddMinutes(-2) },
                _ => new ThermalReading { HeadsetReachable = true, BatteryCelsius = 36, SampledUtc = Now, ThermalStatusText = "status severe" }
            };
        }, (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);

        Assert.Equal(PropertyTransactionStatus.Restored, outcome.RestorationStatus);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.True(guard.AutomaticReapplyBlocked);
    }

    [Fact]
    public void CancellationRestoresTheOwnedOverrideAndBlocksReapply()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
        var outcome = guard.Run("quest", "fp", _ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, cancelled.Token);
        Assert.True(outcome.Cancelled);
        Assert.True(outcome.Restored);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.True(guard.AutomaticReapplyBlocked);
        Assert.Contains("cannot remain active", outcome.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupersededWatchDoesNotRestore()
    {
        var writes = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(36), (_, _) =>
        {
            writes++;
            throw new InvalidOperationException("restore");
        });
        var outcome = guard.Run(
            "quest",
            "fp",
            _ => Sample(36),
            (_, _) =>
            {
                writes++;
                throw new InvalidOperationException("restore");
            },
            () => Now,
            (_, _) => { },
            cancelled.Token,
            stillOwner: () => false);
        Assert.True(outcome.Cancelled);
        Assert.False(outcome.Restored);
        Assert.Equal(0, writes);
        Assert.Contains("superseded", outcome.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnexpectedReadFailureRestores()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
        var outcome = guard.Run("quest", "fp", _ => throw new InvalidCastException("probe"), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);
        Assert.True(outcome.Restored);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.True(guard.AutomaticReapplyBlocked);
    }

    [Fact]
    public void ExternalGpuChangeIsLeftInPlace()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        store.Set("quest", "debug.oculus.gpuLevel", "5");
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(50), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
        var outcome = guard.Run("quest", "fp", _ => Sample(50), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);
        Assert.Equal(PropertyTransactionStatus.Conflict, outcome.RestorationStatus);
        Assert.Equal("5", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.True(guard.AutomaticReapplyBlocked);
        Assert.False(outcome.Restored);
    }

    [Fact]
    public void SampleBudgetRestores()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        var guard = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
        var outcome = guard.Run("quest", "fp", _ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);
        Assert.True(outcome.Restored);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.Contains("sample budget", outcome.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShutdownAndCrashLeaseRestoreTheOwnedOverride()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        var path = Path.Combine(Path.GetTempPath(), "mqt-thermal-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var guard = new ExperimentalGpuThermalGuard(
                _ => Sample(36),
                (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]),
                latchPath: path);
            guard.BeginExplicit("quest", "Quest 3", "fp");
            var stopped = guard.RestoreOwnedAndStop("Application shutting down.", TimeSpan.FromSeconds(5));
            Assert.True(stopped.Restored);
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.True(guard.AutomaticReapplyBlocked);

            store.Set("quest", "debug.oculus.gpuLevel", "4");
            Assert.Equal(PropertyTransactionStatus.Verified, service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" }).Status);
            var crashed = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), latchPath: path);
            crashed.BeginExplicit("quest", "Quest 3", "fp");
            crashed.StopWithoutRestore("process disappeared");
            var restarted = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), latchPath: path);
            var recovered = restarted.RecoverPendingLease();
            Assert.NotNull(recovered);
            Assert.True(recovered!.Restored);
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.True(restarted.AutomaticReapplyBlocked);
            var blocked = restarted.Run("quest", "fp", _ => Sample(30), (_, _) => throw new InvalidOperationException("reapply"), () => Now, (_, _) => { }, CancellationToken.None);
            Assert.True(blocked.BlockedAutomaticReapply);

            store.Set("quest", "debug.oculus.gpuLevel", "4");
            Assert.Equal(PropertyTransactionStatus.Verified, service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" }).Status);
            var unwitnessed = new ExperimentalGpuThermalGuard(_ => Sample(36), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]));
            var fromSnapshot = unwitnessed.RecoverSnapshot("quest", "fp");
            Assert.NotNull(fromSnapshot);
            Assert.True(fromSnapshot!.Restored);
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.True(unwitnessed.AutomaticReapplyBlocked);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void FailedRestorationIsRecordedAndStillBlocksReapply()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
        store.FailWrite = true;
        var path = Path.Combine(Path.GetTempPath(), "mqt-thermal-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var guard = new ExperimentalGpuThermalGuard(_ => Sample(50), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), latchPath: path);
            var outcome = guard.Run("quest", "fp", _ => Sample(50), (serial, fingerprint) => service.Restore(serial, fingerprint, ["debug.oculus.gpuLevel"]), () => Now, (_, _) => { }, CancellationToken.None);
            Assert.Equal(PropertyTransactionStatus.Incomplete, outcome.RestorationStatus);
            Assert.Contains("failure was recorded", outcome.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("7", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.True(File.Exists(path));

            var reloaded = new ExperimentalGpuThermalGuard(_ => throw new InvalidOperationException("no read"), (_, _) => throw new InvalidOperationException("no restore"), latchPath: path);
            var blocked = reloaded.Run("quest", "fp", _ => throw new InvalidOperationException("no read"), (_, _) => throw new InvalidOperationException("no restore"), () => Now, (_, _) => { }, CancellationToken.None);
            Assert.True(blocked.BlockedAutomaticReapply);
            reloaded.AcknowledgeExplicitApply();
            Assert.False(reloaded.AutomaticReapplyBlocked);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SelectedGpuRestoreLeavesAnUnrelatedPropertyApplied()
    {
        var store = new MemoryPropertyStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        store.Set("quest", "debug.oculus.refreshRate", "90");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel"] = "7",
            ["debug.oculus.refreshRate"] = "120"
        });
        var restored = service.Restore("quest", "fp", ["debug.oculus.gpuLevel"]);
        Assert.Equal(PropertyTransactionStatus.Restored, restored.Status);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
        Assert.Equal("120", store.Read("quest", "debug.oculus.refreshRate"));
        Assert.Contains("still tracks", restored.Summary, StringComparison.OrdinalIgnoreCase);
        var full = service.Restore("quest", "fp");
        Assert.Equal(PropertyTransactionStatus.Restored, full.Status);
        Assert.Equal("90", store.Read("quest", "debug.oculus.refreshRate"));
    }

    private static ExperimentalGpuThermalGuard Guard(Func<string, ThermalReading> read, Func<string, string?, PropertyTransactionResult> restore) =>
        new(read, restore);

    private static ThermalReading Sample(double? celsius) => new()
    {
        HeadsetReachable = true,
        BatteryCelsius = celsius,
        SampledUtc = Now
    };

    private sealed class MemoryPropertyStore : IHeadsetPropertyStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public bool FailWrite { get; set; }

        public void Set(string serial, string name, string value) => _values[serial + "|" + name] = value;

        public string? Read(string serial, string name) =>
            _values.TryGetValue(serial + "|" + name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

        public void Write(string serial, string name, string value)
        {
            if (FailWrite)
            {
                throw new IOException("injected");
            }

            Set(serial, name, value);
        }
    }
}
