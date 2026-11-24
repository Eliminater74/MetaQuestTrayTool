using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public sealed class DesktopNotificationTests
{
    private static readonly NoticePreferences On = new();

    [Fact]
    public void StartupNoticeIsBrandedAndDoesNotUseTheOldBalloonText()
    {
        var item = new DesktopNotificationCenter().PresentStartup(On, desktopEnabled: true, DateTimeOffset.UnixEpoch);

        Assert.NotNull(item);
        Assert.Equal("Ready for Virtual Reality", item.Title);
        Assert.Equal("Your PCVR controls and performance tools are ready.", item.Message);
        Assert.Equal([NoticeAction.OpenSettings, NoticeAction.OpenPerformance], item.Actions);
        Assert.DoesNotContain("Right-click the headset icon", item.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernDefaultDoesNotRequireABalloon()
    {
        Assert.True(new NoticeSettingsCheck().ModernFlyouts);
        var item = new DesktopNotificationCenter().Present("Profile", "Applied.", On, true, DateTimeOffset.UnixEpoch);
        Assert.NotNull(item);
        Assert.Equal(NoticeSeverity.Success, item.Severity);
        Assert.Equal(NoticeAction.OpenProfiles, item.Action);
    }

    [Fact]
    public void RepeatedNoticesAreSuppressedAndHistoryStaysBounded()
    {
        var center = new DesktopNotificationCenter();
        var first = center.Present("Headset", "Connected.", On, true, DateTimeOffset.UnixEpoch);
        var second = center.Present("Headset", "Connected.", On, true, DateTimeOffset.UnixEpoch.AddSeconds(2));
        Assert.NotNull(first);
        Assert.Null(second);

        for (var i = 0; i < 50; i++)
        {
            center.Present("Update " + i, "Ready.", On, true, DateTimeOffset.UnixEpoch.AddMinutes(i + 1));
        }

        Assert.True(center.History.Count <= DesktopNotificationCenter.MaxHistory);
        Assert.True(center.Visible.Count <= DesktopNotificationCenter.MaxVisible);
    }

    [Fact]
    public void DisabledDesktopStillKeepsARecoveryWarning()
    {
        var item = new DesktopNotificationCenter().Present("PCVR recover", "Restore failed.", On, desktopEnabled: false, DateTimeOffset.UnixEpoch);
        Assert.NotNull(item);
        Assert.Equal(NoticeCategory.Recovery, item.Category);
        Assert.Equal(NoticeSeverity.Error, item.Severity);
    }

    [Fact]
    public void PlacementStaysInsideTheWorkArea()
    {
        var anchor = NoticePlacement.Place(0, 0, 1920, 1080, 380, 160, 2, taskbarOnTop: false, taskbarOnLeft: false);
        Assert.True(anchor.Left >= 0);
        Assert.True(anchor.Top >= 0);
        Assert.True(anchor.Left + 380 <= 1920);
        Assert.True(anchor.Top + 160 <= 1080);
    }

    private sealed class NoticeSettingsCheck
    {
        public bool ModernFlyouts { get; } = new MetaQuestTrayTool.Models.NoticeSettings().ModernFlyouts;
    }
}
