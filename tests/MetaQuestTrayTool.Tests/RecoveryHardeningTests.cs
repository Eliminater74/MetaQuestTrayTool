using System.Text.Json;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class RecoveryHardeningTests
{
    [Fact]
    public void CorruptRecoveryFileIsNotOverwrittenUntilQuarantined()
    {
        var store = new RecordingStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "{");
            var service = new AdbPropertyTransactionService(store, path);
            var blocked = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            });
            Assert.Equal(PropertyTransactionStatus.Failed, blocked.Status);
            Assert.Contains("corrupt", blocked.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("{", File.ReadAllText(path));
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.Empty(store.WriteNames);

            var moved = service.QuarantineCorruptRecovery();
            Assert.Equal(PropertyTransactionStatus.Restored, moved.Status);
            Assert.False(File.Exists(path));
            Assert.Equal("{", File.ReadAllText(path + ".corrupt"));
            Assert.Empty(store.WriteNames);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void CrashMidApplyRestoresTheWrittenPropertyAndKeepsTheOriginal()
    {
        var store = new RecordingStore();
        store.Set("quest", "debug.oculus.cpuLevel", "4");
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var path = TempFile();
        try
        {
            var snapshot = new AdbPropertySnapshot
            {
                Serial = "quest",
                Model = "Quest 3",
                Fingerprint = "fp",
                BootId = "boot-1",
                Owner = AdbPropertyTransactionService.OwnerId,
                CapturedUtc = DateTimeOffset.UtcNow,
                Properties =
                [
                    new TrackedHeadsetProperty
                    {
                        Name = "debug.oculus.cpuLevel",
                        OriginalPresence = PropertyPresence.Present,
                        OriginalValue = "2",
                        AppliedValue = "4"
                    },
                    new TrackedHeadsetProperty
                    {
                        Name = "debug.oculus.gpuLevel",
                        OriginalPresence = PropertyPresence.Present,
                        OriginalValue = "4",
                        AppliedValue = "7"
                    }
                ]
            };
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
            var service = new AdbPropertyTransactionService(store, path);
            var restored = service.Restore("quest", "fp");
            Assert.Equal(PropertyTransactionStatus.Restored, restored.Status);
            Assert.Equal("2", store.Read("quest", "debug.oculus.cpuLevel"));
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.DoesNotContain(store.WriteNames, name => name.Contains("texture", StringComparison.Ordinal));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void RebootExternalChangeAndDisconnectDoNotOverwriteUnrelatedState()
    {
        var store = new RecordingStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        store.Set("quest", "debug.oculus.textureWidth", "1440");
        var path = TempFile();
        try
        {
            var service = new AdbPropertyTransactionService(store, path);
            Assert.Equal(PropertyTransactionStatus.Verified, service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            }, bootId: "boot-1").Status);

            var otherBoot = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "5"
            }, bootId: "boot-2");
            Assert.Equal(PropertyTransactionStatus.Failed, otherBoot.Status);
            Assert.Equal("7", store.Read("quest", "debug.oculus.gpuLevel"));

            store.Set("quest", "debug.oculus.gpuLevel", "9");
            var conflict = service.Restore("quest", "fp", null, "boot-2");
            Assert.Equal(PropertyTransactionStatus.Conflict, conflict.Status);
            Assert.Equal("9", store.Read("quest", "debug.oculus.gpuLevel"));
            Assert.Equal("1440", store.Read("quest", "debug.oculus.textureWidth"));
            Assert.DoesNotContain("debug.oculus.textureWidth", store.WriteNames);

            store.Set("quest", "debug.oculus.gpuLevel", "");
            var cleared = service.Restore("quest", "fp", null, "boot-2");
            Assert.Equal(PropertyTransactionStatus.Restored, cleared.Status);
            Assert.True(string.IsNullOrEmpty(store.Read("quest", "debug.oculus.gpuLevel")));
            Assert.Contains("reboot", cleared.ToDisplayText(), StringComparison.OrdinalIgnoreCase);

            service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            }, bootId: "boot-3");
            store.ThrowOnRead = true;
            var disconnected = service.Restore("quest", "fp", null, "boot-3");
            Assert.Equal(PropertyTransactionStatus.Failed, disconnected.Status);
            Assert.True(File.Exists(path));
            Assert.Equal("7", store.Values["quest|debug.oculus.gpuLevel"]);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void DuplicateRestoreDifferentOwnerAndBusyOperationAreRefused()
    {
        var store = new RecordingStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var path = TempFile();
        try
        {
            var service = new AdbPropertyTransactionService(store, path);
            service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" });
            Assert.Equal(PropertyTransactionStatus.Restored, service.Restore("quest", "fp").Status);
            var second = service.Restore("quest", "fp");
            Assert.Equal(PropertyTransactionStatus.Failed, second.Status);
            Assert.Contains("No property snapshot", second.Summary, StringComparison.Ordinal);

            var foreign = new AdbPropertySnapshot
            {
                Serial = "quest",
                Owner = "sidequest",
                CapturedUtc = DateTimeOffset.UtcNow,
                Properties =
                [
                    new TrackedHeadsetProperty
                    {
                        Name = "debug.oculus.gpuLevel",
                        OriginalPresence = PropertyPresence.Present,
                        OriginalValue = "4",
                        AppliedValue = "7"
                    }
                ]
            };
            File.WriteAllText(path, JsonSerializer.Serialize(foreign));
            var owned = new AdbPropertyTransactionService(store, path);
            var refused = owned.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "6" });
            Assert.Equal(PropertyTransactionStatus.Failed, refused.Status);
            Assert.Contains("owner", refused.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));

            Assert.True(owned.TryBeginOperation(out var generation));
            try
            {
                var busy = new AdbPropertyTransactionService(store, path);
                var blocked = busy.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "6" });
                Assert.Equal(PropertyTransactionStatus.Failed, blocked.Status);
                Assert.Contains("in progress", blocked.Summary, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                owned.EndOperation(generation);
            }

            Assert.True(owned.TryBeginOperation(out var stale));
            owned.EndOperation(stale);
            Assert.True(owned.TryBeginOperation(out var current));
            try
            {
                var late = owned.ApplyIfCurrent(stale, "quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "6" });
                Assert.Equal(PropertyTransactionStatus.Failed, late.Status);
                Assert.Contains("stale", late.Summary, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
            }
            finally
            {
                owned.EndOperation(current);
            }
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void SurvivedRebootRestoresTheOriginalValue()
    {
        var store = new RecordingStore();
        store.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(store);
        service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string> { ["debug.oculus.gpuLevel"] = "7" }, bootId: "boot-1");
        var restored = service.Restore("quest", "fp", null, "boot-2");
        Assert.Equal(PropertyTransactionStatus.Restored, restored.Status);
        Assert.Equal("4", store.Read("quest", "debug.oculus.gpuLevel"));
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "mqt-recover-" + Guid.NewGuid().ToString("N") + ".json");

    private static void Delete(string path)
    {
        foreach (var suffix in new[] { "", ".tmp", ".bak", ".corrupt" })
        {
            try
            {
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class RecordingStore : IHeadsetPropertyStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public List<string> WriteNames { get; } = [];
        public bool ThrowOnRead { get; set; }

        public void Set(string serial, string name, string value) => Values[serial + "|" + name] = value;

        public string? Read(string serial, string name)
        {
            if (ThrowOnRead)
            {
                throw new IOException("disconnected");
            }

            return Values.TryGetValue(serial + "|" + name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
        }

        public void Write(string serial, string name, string value)
        {
            WriteNames.Add(name);
            Set(serial, name, value);
        }
    }
}
