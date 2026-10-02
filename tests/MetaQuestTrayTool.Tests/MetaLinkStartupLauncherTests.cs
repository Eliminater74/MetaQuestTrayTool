using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class MetaLinkStartupLauncherTests
{
    [Fact]
    public void OpenMetaLinkOnStartDefaultsToFalse()
    {
        Assert.False(new TrayToolSettings().OpenMetaLinkOnStart);
    }

    [Fact]
    public void DisabledStartupDoesNotOpenMetaHorizonLink()
    {
        var calls = 0;
        MetaLinkStartupLauncher.LaunchIfEnabled(
            enabled: false,
            open: () =>
            {
                calls++;
                return "opened";
            },
            info: _ => calls += 10,
            warn: _ => calls += 100);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void EnabledStartupOpensOnceAndLogsTheResult()
    {
        string? logged = null;
        var calls = 0;
        MetaLinkStartupLauncher.LaunchIfEnabled(
            enabled: true,
            open: () =>
            {
                calls++;
                return "Brought Meta Horizon Link to the foreground.";
            },
            info: message => logged = message,
            warn: _ => logged = "warn");

        Assert.Equal(1, calls);
        Assert.Equal("Brought Meta Horizon Link to the foreground.", logged);
    }

    [Fact]
    public void StartupFailureIsLoggedAndDoesNotThrow()
    {
        string? warning = null;
        var exception = Record.Exception(() =>
            MetaLinkStartupLauncher.LaunchIfEnabled(
                enabled: true,
                open: () => throw new InvalidOperationException("client missing"),
                info: _ => { },
                warn: message => warning = message));

        Assert.Null(exception);
        Assert.Contains("client missing", warning);
        Assert.Contains("Could not open Meta Horizon Link on startup", warning);
    }

    [Fact]
    public void MissingClientMessageIsAWarning()
    {
        string? warning = null;
        MetaLinkStartupLauncher.LaunchIfEnabled(
            enabled: true,
            open: () => "Meta Horizon Link client was not found (expected Support\\oculus-client\\Client.exe).",
            info: _ => warning = "info",
            warn: message => warning = message);

        Assert.Contains("not found", warning);
    }
}
