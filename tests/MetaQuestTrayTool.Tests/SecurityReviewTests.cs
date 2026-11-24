using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class SecurityReviewTests
{
    [Fact]
    public void OverlayRejectsMissingTokenWrongTokenAndOversizedInput()
    {
        const string token = "0123456789abcdef0123456789abcdef";
        Assert.Contains("auth required", OverlayCommands.Handle("ping", null, null), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("auth rejected", OverlayCommands.Handle("auth 00000000000000000000000000000000 ping", null, token), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("malformed", OverlayCommands.Handle(new string('a', 1025), null, token), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tray-loads-openvr=false", OpenVrOverlayStatus.Describe(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AlvrRefusesEnumValuesTraversalAndPyrowaveFiles()
    {
        const string enumJson = """{"session_settings":{"video":{"preferred_fps":{"variant":"Custom"}}},"extra":"keep"}""";
        Assert.Null(AlvrSessionEditor.TryApplyAllowlisted(enumJson, AlvrSettingCatalog.PreferredFps.Path, 90, false, out var detail));
        Assert.Contains("plain number", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep", enumJson, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => AlvrSessionEditor.WriteWithBackup(@"C:\ALVR\..\session.json", "{}"));
    }

    [Fact]
    public void ProfileDecisionDoesNotLaunchOrWriteProviderSettings()
    {
        var decision = PcvrProfilePolicy.Decide(new PcvrProfilePreferences
        {
            PreferredProvider = "Pyrowave",
            AutoSwitchProvider = false,
            ExperimentalGpuMaximum = true,
            ExperimentalRefreshHz = 207,
            PyrowaveProfile = "Quality 120 Hz"
        });
        Assert.Contains("No streaming provider was launched", decision.Summary, StringComparison.Ordinal);
        Assert.Contains("did not write", decision.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("were not applied", decision.Summary, StringComparison.OrdinalIgnoreCase);

        var actions = new RecordingProfileActions();
        var applied = new PcvrProfileLifecycle().Apply(decisionPreferences(), ReadyProfile(), actions);
        Assert.Empty(actions.Writes);
        Assert.Contains("was not written", applied.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("were not applied", applied.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static PcvrProfilePreferences decisionPreferences() => new()
    {
        PreferredProvider = "Pyrowave",
        AutoSwitchProvider = false,
        ExperimentalGpuMaximum = true,
        ExperimentalRefreshHz = 207,
        PyrowaveProfile = "Quality 120 Hz"
    };

    private static PcvrProfileContext ReadyProfile() => new()
    {
        SessionActive = true,
        ActiveProvider = "Meta Quest Link",
        TrustedSerial = "trusted",
        HeadsetSerial = "trusted",
        AdbSerial = "serial-1",
        Model = "Quest 3",
        Fingerprint = "fingerprint",
        HeadsetReady = true,
        ExperimentalGpuOptIn = true,
        BatteryCelsius = 40
    };

    private sealed class RecordingProfileActions : IPcvrProfileActions
    {
        public List<string> Writes { get; } = [];
        public bool OwnsRestore => false;
        public string ApplyExperimentalGpu(PcvrProfileContext context)
        {
            Writes.Add(context.AdbSerial ?? string.Empty);
            return "written";
        }

        public string RestoreExperimentalGpu(string serial) => "restored";
    }

    [Fact]
    public void AllowlistedSessionWriteKeepsABackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mqt-alvr-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "session.json");
        try
        {
            Directory.CreateDirectory(directory);
            const string original = """{"session_settings":{"video":{"preferred_fps":72}},"extra":"keep me"}""";
            File.WriteAllText(path, original);
            var edited = AlvrSessionEditor.TryApplyAllowlisted(original, AlvrSettingCatalog.PreferredFps.Path, 90, false, out _);
            Assert.NotNull(edited);
            AlvrSessionEditor.WriteWithBackup(path, edited!);
            Assert.Equal(edited, File.ReadAllText(path));
            Assert.Equal(original, File.ReadAllText(path + ".bak"));
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
            catch (IOException)
            {
            }
        }
    }
}
