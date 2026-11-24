using System.Text.Json;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class AdbPropertyTransactionTests
{
    [Fact]
    public void ApplyReadsBackAndRestoreReturnsTheOriginalValue()
    {
        var client = new FakePropertyStore();
        client.Set("quest", "debug.oculus.gpuLevel", "4");
        var service = new AdbPropertyTransactionService(client);
        var applied = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel"] = "7"
        });
        Assert.Equal(PropertyTransactionStatus.Verified, applied.Status);
        Assert.Equal("7", client.Read("quest", "debug.oculus.gpuLevel"));

        var restored = service.Restore("quest", "fp");
        Assert.Equal(PropertyTransactionStatus.Restored, restored.Status);
        Assert.Equal("4", client.Read("quest", "debug.oculus.gpuLevel"));
        Assert.Contains("readback matched", applied.ToDisplayText(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInjectionAndWritesNothing()
    {
        var client = new FakePropertyStore();
        var service = new AdbPropertyTransactionService(client);
        var result = service.Apply("quest", null, null, new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel;reboot"] = "7"
        });
        Assert.Equal(PropertyTransactionStatus.Failed, result.Status);
        Assert.Equal(0, client.Writes);

        result = service.Apply("quest", null, null, new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel"] = "7\nreboot"
        });
        Assert.Equal(PropertyTransactionStatus.Failed, result.Status);
        Assert.Equal(0, client.Writes);
    }

    [Fact]
    public void DifferentHeadsetCannotRestoreOrDiscard()
    {
        var client = new FakePropertyStore();
        client.Set("quest-a", "debug.oculus.gpuLevel", "4");
        var path = Path.Combine(Path.GetTempPath(), "mqt-prop-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new AdbPropertyTransactionService(client, path);
            Assert.Equal(PropertyTransactionStatus.Verified, service.Apply("quest-a", "Quest 3", "fp-a", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            }).Status);

            var other = service.Restore("quest-b", "fp-a");
            Assert.Equal(PropertyTransactionStatus.Failed, other.Status);
            Assert.Equal("7", client.Read("quest-a", "debug.oculus.gpuLevel"));
            Assert.Equal(PropertyTransactionStatus.Failed, service.DiscardRecovery("quest-b").Status);
            Assert.True(File.Exists(path));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void FingerprintChangeAndExternalConflictDoNotOverwrite()
    {
        var client = new FakePropertyStore();
        client.Set("quest", "debug.oculus.refreshRate", "90");
        var service = new AdbPropertyTransactionService(client);
        service.Apply("quest", "Quest 3", "fp1", new Dictionary<string, string>
        {
            ["debug.oculus.refreshRate"] = "120"
        });

        Assert.Equal(PropertyTransactionStatus.Failed, service.Restore("quest", "fp2").Status);
        Assert.Equal("120", client.Read("quest", "debug.oculus.refreshRate"));

        client.Set("quest", "debug.oculus.refreshRate", "72");
        var conflict = service.Restore("quest", "fp1");
        Assert.Equal(PropertyTransactionStatus.Conflict, conflict.Status);
        Assert.Equal("72", client.Read("quest", "debug.oculus.refreshRate"));
    }

    [Fact]
    public void AbsentOriginalCannotBeDeletedAndPartialWriteRollsBack()
    {
        var client = new FakePropertyStore();
        var service = new AdbPropertyTransactionService(client);
        var absent = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
        {
            ["debug.oculus.gpuLevel"] = "7"
        });
        Assert.Equal(PropertyTransactionStatus.Verified, absent.Status);
        var incomplete = service.Restore("quest", "fp");
        Assert.Equal(PropertyTransactionStatus.Incomplete, incomplete.Status);
        Assert.Contains("Reboot", incomplete.ToDisplayText(), StringComparison.Ordinal);
        Assert.Equal("7", client.Read("quest", "debug.oculus.gpuLevel"));

        client.Set("quest", "debug.oculus.cpuLevel", "2");
        client.Set("quest", "debug.oculus.gpuLevel", "4");
        client.FailWriteName = "debug.oculus.gpuLevel";
        var rolled = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
        {
            ["debug.oculus.cpuLevel"] = "4",
            ["debug.oculus.gpuLevel"] = "7"
        });
        Assert.Equal(PropertyTransactionStatus.RolledBack, rolled.Status);
        Assert.Equal("2", client.Read("quest", "debug.oculus.cpuLevel"));
        Assert.Equal("4", client.Read("quest", "debug.oculus.gpuLevel"));
    }

    [Fact]
    public void ReadbackMismatchRollsBackAndRecoveryFileSurvivesARestart()
    {
        var client = new FakePropertyStore { RejectReadbackName = "debug.oculus.gpuLevel" };
        client.Set("quest", "debug.oculus.gpuLevel", "4");
        var path = Path.Combine(Path.GetTempPath(), "mqt-prop-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new AdbPropertyTransactionService(client, path);
            var result = service.Apply("quest", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            });
            Assert.Equal(PropertyTransactionStatus.RolledBack, result.Status);
            Assert.Equal("4", client.Read("quest", "debug.oculus.gpuLevel"));
            Assert.True(File.Exists(path));

            client.RejectReadbackName = null;
            client.Set("quest", "debug.oculus.gpuLevel", "7");
            var reloaded = new AdbPropertyTransactionService(client, path);
            var snapshot = JsonSerializer.Deserialize<AdbPropertySnapshot>(File.ReadAllText(path));
            Assert.Equal("quest", snapshot!.Serial);
            Assert.Equal(PropertyTransactionStatus.Restored, reloaded.Restore("quest", "fp").Status);
            Assert.Equal("4", client.Read("quest", "debug.oculus.gpuLevel"));
            Assert.False(File.Exists(path));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DisconnectDuringWriteDoesNotTouchADifferentSerialSnapshot()
    {
        var client = new FakePropertyStore();
        client.Set("quest-a", "debug.oculus.gpuLevel", "4");
        var path = Path.Combine(Path.GetTempPath(), "mqt-prop-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new AdbPropertyTransactionService(client, path);
            service.Apply("quest-a", "Quest 3", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.gpuLevel"] = "7"
            });
            client.ThrowOnWrite = true;
            var blocked = service.Apply("quest-b", "Quest 2", "fp", new Dictionary<string, string>
            {
                ["debug.oculus.cpuLevel"] = "2"
            });
            Assert.Equal(PropertyTransactionStatus.Failed, blocked.Status);
            Assert.Equal("7", client.Read("quest-a", "debug.oculus.gpuLevel"));
            Assert.Null(client.Read("quest-b", "debug.oculus.cpuLevel"));
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakePropertyStore : IHeadsetPropertyStore
    {
        private readonly Dictionary<string, Dictionary<string, string>> _values = new(StringComparer.Ordinal);
        public int Writes { get; private set; }
        public string? FailWriteName { get; set; }
        public string? RejectReadbackName { get; set; }
        public bool ThrowOnWrite { get; set; }
        private bool _lieOnce;
        private int _liesLeft = 1;

        public void Set(string serial, string name, string value)
        {
            if (!_values.TryGetValue(serial, out var props))
            {
                props = new Dictionary<string, string>(StringComparer.Ordinal);
                _values[serial] = props;
            }

            props[name] = value;
        }

        public string? Read(string serial, string name)
        {
            if (_lieOnce && RejectReadbackName == name)
            {
                _lieOnce = false;
                return "rejected";
            }

            return _values.TryGetValue(serial, out var props) && props.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }

        public void Write(string serial, string name, string value)
        {
            Writes++;
            if (ThrowOnWrite || FailWriteName == name)
            {
                throw new IOException("injected failure");
            }

            Set(serial, name, value);
            if (RejectReadbackName == name && _liesLeft > 0)
            {
                _lieOnce = true;
                _liesLeft--;
            }
        }
    }
}
