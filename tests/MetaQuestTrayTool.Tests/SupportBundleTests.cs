using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class SupportBundleTests
{
    [Fact]
    public void SupportSanitizerRedactsPathsNetworkAndDeviceTokens()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var input =
            $"Settings: {userProfile}\\AppData\\Roaming\\MetaQuestTrayTool\\settings.json\n" +
            "Hardware serial: 1WMHH123456789\n" +
            "ADB endpoint: 192.168.1.40:5555\n" +
            "Wi-Fi SSID: \"Living Room VR\"\n" +
            "Fingerprint: meta/quest/secret-build\n" +
            "Unlabeled token QUEST3ABC12345";

        var sanitized = SupportBundleService.SanitizeForSupport(input);

        Assert.DoesNotContain(userProfile, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1WMHH123456789", sanitized);
        Assert.DoesNotContain("192.168.1.40", sanitized);
        Assert.DoesNotContain("Living Room VR", sanitized);
        Assert.DoesNotContain("meta/quest/secret-build", sanitized);
        Assert.DoesNotContain("QUEST3ABC12345", sanitized);
        Assert.Contains("%USERPROFILE%", sanitized);
        Assert.Contains("<ip>", sanitized);
    }

    [Fact]
    public void SettingsSummaryUsesCountsInsteadOfProfileOrCommandText()
    {
        var settings = new AppSettings();
        settings.Profiles.Add(new GameProfile
        {
            Name = "Secret Game",
            ProcessName = "secretgame",
            InstallPath = @"C:\Users\tester\Games\Secret Game"
        });
        settings.CustomCommands.CliCommands.Add("server:set-secret 1234");
        settings.CustomCommands.AdbCommands.Add("setprop debug.secret 1");
        settings.Headset.CustomAdbCommands.Add("setprop debug.oculus.secret 1");

        var summary = SupportBundleService.BuildSettingsSummary(settings);

        Assert.Contains("Profiles count: 1", summary);
        Assert.Contains("Global CLI custom command count: 1", summary);
        Assert.Contains("Global ADB custom command count: 1", summary);
        Assert.Contains("Headset ADB custom command count: 1", summary);
        Assert.DoesNotContain("Secret Game", summary);
        Assert.DoesNotContain("secretgame", summary);
        Assert.DoesNotContain("set-secret", summary);
        Assert.DoesNotContain("debug.secret", summary);
        Assert.DoesNotContain("InstallPath", summary);
    }
}
