using System.Text.Json;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PcvrIntegrationTests
{
    [Theory]
    [InlineData("ping", "OK pong")]
    [InlineData("overlay-status", "openvr-overlay=not-loaded")]
    [InlineData("benchmark-start 30", "SteamVR was not restarted")]
    [InlineData("benchmark-start 15", "ERR unsupported")]
    [InlineData("profile Beat Saber", "ERR profile")]
    [InlineData("ping; adb shell", "ERR command rejected")]
    [InlineData("cmd /c whoami", "ERR command rejected")]
    public void OverlayCommandsStayOnTheAllowList(string command, string expectedFragment) =>
        Assert.Contains(expectedFragment, OverlayCommands.Handle(command, () => "secret"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void SteamVrOverMetaLinkIsNotTheSteamLinkApp()
    {
        var results = ProviderClassifier.Classify(new ProviderSignals
        {
            MetaLinkStreaming = true,
            SteamVrRunning = true,
            SteamLinkProcess = true
        });
        Assert.Contains(results, item => item.Kind == PcvrProviderKind.MetaQuestLink && item.ActiveStream);
        Assert.Contains(results, item => item.Kind == PcvrProviderKind.SteamVr && item.Detail.Contains("not the Steam Link", StringComparison.Ordinal));
        Assert.DoesNotContain(results, item => item.Kind == PcvrProviderKind.SteamLink);
    }

    [Fact]
    public void AlvrEditPreservesUnknownFieldsAndRefusesARunningServer()
    {
        const string json = """{"video":{"bitrate_mbps":150},"extra":"keep me"}""";
        var blocked = AlvrSessionEditor.TrySetExistingNumber(json, "video.bitrate_mbps", 200, serverRunning: true, allowWhileRunning: false, out var blockedDetail);
        Assert.Null(blocked);
        Assert.Contains("running", blockedDetail, StringComparison.OrdinalIgnoreCase);

        var edited = AlvrSessionEditor.TrySetExistingNumber(json, "video.bitrate_mbps", 200, false, false, out var detail);
        Assert.Contains("preserved", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep me", edited, StringComparison.Ordinal);
        Assert.Contains("200", edited, StringComparison.Ordinal);
        Assert.Null(AlvrSessionEditor.TrySetExistingNumber(json, "video.does_not_exist", 1, false, false, out _));
        Assert.False(AlvrSessionEditor.IsSafeDirectory(@"C:\ALVR\..\Windows"));
    }

    [Fact]
    public void DriverRegistrationAddsAndNeverRemoves()
    {
        const string json = """{"external_drivers":["C:\\VirtualDesktop"],"runtime":["keep"]}""";
        Assert.Throws<InvalidOperationException>(() => SteamVrDriverList.Add(json, @"C:\ALVR\driver", authorized: false));
        var updated = SteamVrDriverList.Add(json, @"C:\ALVR\driver", authorized: true);
        var drivers = SteamVrDriverList.Read(updated);
        Assert.Contains(@"C:\VirtualDesktop", drivers);
        Assert.Contains(@"C:\ALVR\driver", drivers);
        Assert.Contains("keep", updated, StringComparison.Ordinal);
        Assert.Equal(2, drivers.Count);
    }

    [Fact]
    public void PyrowaveProfilesComeFromTheSessionTextAndTheApkIsNotInstalled()
    {
        Assert.Equal("io.github.jms1717.quest3pyrowave", PyrowaveDiscovery.PackageName);
        Assert.True(PyrowaveDiscovery.LooksLikeInstall(@"C:\Games\Quest3-Pyrowave", null));
        Assert.False(PyrowaveDiscovery.LooksLikeInstall(@"C:\Games\ALVR", "20.12.0"));
        Assert.True(PyrowaveDiscovery.LooksLikeInstall(null, "20.13.0-quest3.pyro.136"));
        var found = PyrowaveDiscovery.DiscoverProfiles("preset Quality 120 Hz only");
        Assert.Equal(["Quality 120 Hz"], found);
        Assert.Empty(PyrowaveDiscovery.DiscoverProfiles(null));
    }

    [Fact]
    public void OldProfilesRemainValidAndAutoSwitchIsIgnored()
    {
        var profile = JsonSerializer.Deserialize<GameProfile>("""{"Name":"Beat","ProcessName":"beat"}""");
        Assert.Null(profile!.Pcvr);
        profile.Pcvr = new PcvrProfilePreferences { PreferredProvider = "ALVR", AutoSwitchProvider = true, ExperimentalRefreshHz = 240 };
        var clone = profile.Clone();
        Assert.True(clone.Pcvr!.AutoSwitchProvider);
        Assert.Contains("ignored", PcvrProfilePolicy.Describe(clone.Pcvr), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not applied", PcvrProfilePolicy.Describe(new PcvrProfilePreferences { ExperimentalRefreshHz = 240 }), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticReportRedactsTheSerial()
    {
        var text = DiagnosticReportBuilder.Build("Headset SERIAL123 changed settings", "SERIAL123");
        Assert.DoesNotContain("SERIAL123", text, StringComparison.Ordinal);
        Assert.Contains("<serial>", text, StringComparison.Ordinal);
        Assert.Contains("not loaded", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not written", text, StringComparison.OrdinalIgnoreCase);
    }
}
