using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class WindowsHdrServiceTests
{
    [Fact]
    public void NewHdrAndStartupOptionsDefaultToFalse()
    {
        var tray = new TrayToolSettings();
        Assert.False(tray.OpenMetaLinkOnStart);
        Assert.False(tray.DisableWindowsHdrWhileRunning);
    }

    [Fact]
    public void EnabledHdrIsDisabledAndRestored()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));

        var disabled = scope.Service.DisableForSession();
        Assert.True(disabled.Succeeded);
        Assert.Contains("HDR temporarily disabled on Dell.", disabled.Summary);
        Assert.False(scope.Displays[0].AdvancedColorEnabled);
        Assert.Equal([(Id(1), false)], scope.Sets);
        Assert.True(File.Exists(scope.SnapshotPath));

        var restored = scope.Service.RestoreSession();
        Assert.True(restored.Succeeded);
        Assert.Contains("HDR restored on Dell.", restored.Summary);
        Assert.True(scope.Displays[0].AdvancedColorEnabled);
        Assert.False(File.Exists(scope.SnapshotPath));
    }

    [Fact]
    public void AlreadyDisabledHdrIsNotTurnedOnAtExit()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: false));

        var disabled = scope.Service.DisableForSession();
        Assert.True(disabled.Succeeded);
        Assert.Contains("HDR was already disabled; no change.", disabled.Summary);
        Assert.Empty(scope.Sets);

        var restored = scope.Service.RestoreSession();
        Assert.True(restored.Succeeded);
        Assert.Equal(string.Empty, restored.Summary);
        Assert.False(scope.Displays[0].AdvancedColorEnabled);
        Assert.Empty(scope.Sets);
    }

    [Fact]
    public void OnlyDisplaysChangedByThisAppAreRestored()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));
        scope.Displays.Add(Display(2, "LG", supported: true, enabled: false));
        scope.Displays.Add(Display(3, "Office", supported: false, enabled: false));

        Assert.True(scope.Service.DisableForSession().Succeeded);
        Assert.Equal([(Id(1), false)], scope.Sets);
        Assert.False(scope.Displays[0].AdvancedColorEnabled);
        Assert.False(scope.Displays[1].AdvancedColorEnabled);
        Assert.False(scope.Displays[2].AdvancedColorEnabled);

        scope.Sets.Clear();
        Assert.True(scope.Service.RestoreSession().Succeeded);
        Assert.Equal([(Id(1), true)], scope.Sets);
        Assert.True(scope.Displays[0].AdvancedColorEnabled);
        Assert.False(scope.Displays[1].AdvancedColorEnabled);
        Assert.False(scope.Displays[2].AdvancedColorEnabled);
    }

    [Fact]
    public void SnapshotIsWrittenBeforeHdrIsChanged()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));
        var sawSnapshot = false;
        scope.BeforeSet = () =>
        {
            sawSnapshot = File.Exists(scope.SnapshotPath)
                          && File.ReadAllText(scope.SnapshotPath).Contains("Dell", StringComparison.Ordinal);
        };

        Assert.True(scope.Service.DisableForSession().Succeeded);
        Assert.True(sawSnapshot);
    }

    [Fact]
    public void StaleSnapshotRestoresTheOriginalDisplayOnNextStart()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));
        Assert.True(scope.Service.DisableForSession().Succeeded);
        Assert.False(scope.Displays[0].AdvancedColorEnabled);

        var recovered = new WindowsHdrService(scope.Fake, scope.SnapshotPath);
        var result = recovered.RecoverStaleSnapshot();
        Assert.True(result.Succeeded);
        Assert.Contains("HDR restored on Dell.", result.Summary);
        Assert.True(scope.Displays[0].AdvancedColorEnabled);
        Assert.False(File.Exists(scope.SnapshotPath));
    }

    [Fact]
    public void StaleSnapshotDoesNotEnableADifferentDisplay()
    {
        using var scope = new HdrScope();
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));
        Assert.True(scope.Service.DisableForSession().Succeeded);

        scope.Displays.Clear();
        scope.Displays.Add(Display(9, "Other", supported: true, enabled: false));
        scope.Sets.Clear();

        var recovered = new WindowsHdrService(scope.Fake, scope.SnapshotPath);
        var result = recovered.RecoverStaleSnapshot();
        Assert.False(result.Succeeded);
        Assert.Contains("does not match the current displays", result.Summary);
        Assert.Empty(scope.Sets);
        Assert.False(scope.Displays[0].AdvancedColorEnabled);
        Assert.True(File.Exists(scope.SnapshotPath));
    }

    [Fact]
    public void QueryAndSetFailuresDoNotThrow()
    {
        using var scope = new HdrScope();
        scope.FailQuery = true;
        var disabled = Record.Exception(() => scope.Service.DisableForSession());
        Assert.Null(disabled);
        var disableResult = scope.Service.DisableForSession();
        Assert.False(disableResult.Succeeded);
        Assert.Contains("HDR control unsupported/failed:", disableResult.Summary);

        scope.FailQuery = false;
        scope.ThrowOnQuery = true;
        var thrown = Record.Exception(() => scope.Service.DisableForSession());
        Assert.Null(thrown);
        Assert.Contains("HDR control unsupported/failed:", scope.Service.DisableForSession().Summary);

        scope.ThrowOnQuery = false;
        scope.Displays.Add(Display(1, "Dell", supported: true, enabled: true));
        scope.FailSet.Add(Id(1));
        var setResult = scope.Service.DisableForSession();
        Assert.False(setResult.Succeeded);
        Assert.Contains("HDR control unsupported/failed: Dell:", setResult.Summary);
        Assert.True(scope.Displays[0].AdvancedColorEnabled);
        Assert.False(File.Exists(scope.SnapshotPath));

        File.WriteAllText(scope.SnapshotPath, """{"Version":1,"App":"MetaQuestTrayTool","Displays":[{"AdapterLuid":4096,"TargetId":1,"FriendlyName":"Dell","WasEnabled":true}]}""");
        scope.FailQuery = true;
        var restored = Record.Exception(() => scope.Service.RestoreSession());
        Assert.Null(restored);
        var restoreResult = new WindowsHdrService(scope.Fake, scope.SnapshotPath).RestoreSession();
        Assert.False(restoreResult.Succeeded);
        Assert.Contains("HDR control unsupported/failed:", restoreResult.Summary);
        Assert.True(File.Exists(scope.SnapshotPath));
    }

    [Fact]
    public void DisplayConfigQueryDoesNotThrow()
    {
        var api = new WindowsDisplayConfigHdrApi();
        var exception = Record.Exception(() =>
        {
            var ok = api.TryQueryActive(out var displays, out var error);
            if (string.Equals(Environment.GetEnvironmentVariable("MQTT_HDR_QUERY"), "1", StringComparison.Ordinal))
            {
                Assert.True(ok, error);
                Assert.NotNull(displays);
            }
        });

        Assert.Null(exception);
    }

    private static HdrDisplayId Id(uint target) => new(0x1000, target);

    private static HdrDisplayState Display(uint target, string name, bool supported, bool enabled) =>
        new(Id(target), name, supported, enabled);

    private sealed class HdrScope : IDisposable
    {
        public HdrScope()
        {
            Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mqtt-hdr-" + Guid.NewGuid().ToString("N")));
            SnapshotPath = Path.Combine(Root.FullName, "hdr-restore.json");
            Fake = new FakeHdrDisplay();
            Service = new WindowsHdrService(Fake, SnapshotPath);
        }

        public DirectoryInfo Root { get; }
        public string SnapshotPath { get; }
        public FakeHdrDisplay Fake { get; }
        public WindowsHdrService Service { get; }
        public List<HdrDisplayState> Displays => Fake.Displays;
        public List<(HdrDisplayId Id, bool Enabled)> Sets => Fake.Sets;
        public HashSet<HdrDisplayId> FailSet => Fake.FailSet;

        public bool FailQuery
        {
            get => Fake.FailQuery;
            set => Fake.FailQuery = value;
        }

        public bool ThrowOnQuery
        {
            get => Fake.ThrowOnQuery;
            set => Fake.ThrowOnQuery = value;
        }

        public Action? BeforeSet
        {
            get => Fake.BeforeSet;
            set => Fake.BeforeSet = value;
        }

        public void Dispose()
        {
            try
            {
                Root.Delete(recursive: true);
            }
            catch
            {
                // Temp cleanup must not hide an assertion.
            }
        }
    }

    private sealed class FakeHdrDisplay : IWindowsHdrDisplay
    {
        public List<HdrDisplayState> Displays { get; } = [];
        public List<(HdrDisplayId Id, bool Enabled)> Sets { get; } = [];
        public HashSet<HdrDisplayId> FailSet { get; } = [];
        public bool FailQuery { get; set; }
        public bool ThrowOnQuery { get; set; }
        public Action? BeforeSet { get; set; }

        public bool TryQueryActive(out IReadOnlyList<HdrDisplayState> displays, out string? error)
        {
            if (ThrowOnQuery)
            {
                throw new InvalidOperationException("display api crashed");
            }

            if (FailQuery)
            {
                displays = Array.Empty<HdrDisplayState>();
                error = "display configuration unavailable";
                return false;
            }

            displays = Displays.ToList();
            error = null;
            return true;
        }

        public bool TrySetAdvancedColor(HdrDisplayId id, bool enabled, out string? error)
        {
            BeforeSet?.Invoke();
            if (FailSet.Contains(id))
            {
                error = "driver rejected the change";
                return false;
            }

            var index = Displays.FindIndex(display => display.Id == id);
            if (index < 0)
            {
                error = "display not found";
                return false;
            }

            Displays[index] = Displays[index] with { AdvancedColorEnabled = enabled };
            Sets.Add((id, enabled));
            error = null;
            return true;
        }
    }
}
