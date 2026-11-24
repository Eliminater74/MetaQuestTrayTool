using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class CompletionSecurityTests
{
    private const string Session = """
    {"session_settings":{"video":{"bitrate":{"mode":{"variant":"ConstantMbps","ConstantMbps":100}}}},"extra":"keep"}
    """;

    [Fact]
    public void CommandShapedProviderAndSessionInputDoesNotWrite()
    {
        Assert.Null(PyrowaveControl.TryBuild("bitrate; calc.exe", "100", true, out var command));
        Assert.Contains("not a verified", command, StringComparison.OrdinalIgnoreCase);
        Assert.Null(PyrowaveControl.TryBuild("wired", "on & restart", true, out var wired));
        Assert.Contains("unavailable", wired, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => PyrowaveControl.Send(
            new QuietTransport(),
            "{\"RestartSteamvr\":true}",
            Session));

        Assert.Null(AlvrSessionManagement.TrySetConstantBitrate(Session, 150, true, out var running));
        Assert.Contains("running", running, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep", Session, StringComparison.Ordinal);
        Assert.Null(AlvrSessionManagement.TrySetResolution(Session, "../session.json", "width", 2048, false, out var traversal));
        Assert.Contains("not one of the two", traversal, StringComparison.OrdinalIgnoreCase);

        var actions = new QuietActions();
        var profile = new PcvrProfileLifecycle().Apply(new PcvrProfilePreferences
        {
            PreferredProvider = "cmd.exe",
            AutoSwitchProvider = true,
            ExperimentalGpuMaximum = true,
            ExperimentalRefreshHz = 240,
            AlvrProfile = "..\\session.json"
        }, new PcvrProfileContext
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
        }, actions);

        Assert.Equal(0, actions.Writes);
        Assert.Contains("does not match", profile.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No streaming provider was launched", profile.Summary, StringComparison.Ordinal);
        Assert.Contains("were not applied", profile.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not written", profile.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class QuietTransport : IPyrowaveTransport
    {
        public string Post(string url, string body) => throw new InvalidOperationException("transport was called");
    }

    private sealed class QuietActions : IPcvrProfileActions
    {
        public int Writes { get; private set; }
        public string ApplyExperimentalGpu(PcvrProfileContext context)
        {
            Writes++;
            return "written";
        }

        public string RestoreExperimentalGpu(string serial) => "restored";
    }
}
