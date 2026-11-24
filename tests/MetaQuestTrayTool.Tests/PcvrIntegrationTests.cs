using System.Text.Json;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PcvrIntegrationTests
{
    [Theory]
    [InlineData("ping", "OK pong")]
    [InlineData("overlay-status", "tray-loads-openvr=false")]
    [InlineData("benchmark-start 30", "SteamVR was not restarted")]
    [InlineData("benchmark-start 15", "ERR unsupported")]
    [InlineData("profile Beat Saber", "ERR profile")]
    [InlineData("ping; adb shell", "ERR command rejected")]
    [InlineData("cmd /c whoami", "ERR command rejected")]
    public void OverlayCommandsStayOnTheAllowList(string command, string expectedFragment)
    {
        const string token = "0123456789abcdef0123456789abcdef";
        var response = OverlayCommands.Handle("auth " + token + " " + command, () => "secret", token);
        Assert.Contains(expectedFragment, response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverlayRequiresTheProcessToken()
    {
        Assert.Contains("auth required", OverlayCommands.Handle("ping"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tray-loads-openvr=false", OpenVrOverlayStatus.Describe(), StringComparison.OrdinalIgnoreCase);
    }

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
        const string json = """{"session_settings":{"video":{"preferred_fps":72}},"extra":"keep me"}""";
        var blocked = AlvrSessionEditor.TryApplyAllowlisted(json, AlvrSettingCatalog.PreferredFps.Path, 90, serverRunning: true, out var blockedDetail);
        Assert.Null(blocked);
        Assert.Contains("running", blockedDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no override", blockedDetail, StringComparison.OrdinalIgnoreCase);

        var edited = AlvrSessionEditor.TryApplyAllowlisted(json, AlvrSettingCatalog.PreferredFps.Path, 90, false, out var detail);
        Assert.Contains("preserved", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep me", edited, StringComparison.Ordinal);
        Assert.Contains("90", edited, StringComparison.Ordinal);
        Assert.Null(AlvrSessionEditor.TryApplyAllowlisted(json, "video.bitrate_mbps", 200, false, out var rejected));
        Assert.Contains("allowlist", rejected, StringComparison.OrdinalIgnoreCase);
        Assert.Null(AlvrSessionEditor.TryApplyAllowlisted(json, AlvrSettingCatalog.PreferredFps.Path, 400, false, out var range));
        Assert.Contains("range", range, StringComparison.OrdinalIgnoreCase);
        const string pyro = """{"session_settings":{"video":{"preferred_fps":72}},"note":"pyrowave"}""";
        Assert.Null(AlvrSessionEditor.TryApplyAllowlisted(pyro, AlvrSettingCatalog.PreferredFps.Path, 90, false, out var pyroDetail));
        Assert.Contains("Pyrowave", pyroDetail, StringComparison.Ordinal);
        Assert.False(AlvrSessionEditor.IsSafeDirectory(@"C:\ALVR\..\Windows"));
        Assert.False(AlvrSessionEditor.TryLaunchDashboard(@"C:\ALVR", authorized: false, out var launch));
        Assert.Contains("not authorized", launch, StringComparison.OrdinalIgnoreCase);
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
