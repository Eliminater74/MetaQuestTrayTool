using System.Text.Json;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class LinkDetectionTests
{
    private static object Headset(string id, long seen, string connection, string operational,
        string primary = "alternate", string type = "headset") => new
    {
        type, serialNumber = id, lastSeenAt = seen, connectionState = connection,
        rdConnectionState = "disconnected", operationalState = operational,
        primaryState = primary, powerState = "active", isUsingAirLink = true
    };

    private static LinkConnectionProbeService.HeadsetCacheEntry? Select(params object[] entries)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { devices = entries }));
        return LinkConnectionProbeService.ParseHeadsetCache(doc.RootElement);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LiveMetaHeadsetWinsOverNewerIdleOrWeakEntryInEitherOrder(bool reverse, bool weak)
    {
        var live = Headset("live", 100, "connected", "operable", "primary");
        var other = weak
            ? Headset("other", 200, "connected", "unknown")
            : Headset("other", 200, "disconnected", "inoperable");
        var selected = reverse ? Select(other, live) : Select(live, other);
        Assert.Equal("live", selected?.SerialNumber);
        // This is the exact gate ProbeCore evaluates before its SteamVR fallback.
        Assert.True(LinkConnectionProbeService.LooksLikeStrongMetaSession(
            selected, metaHmd: false, audioLink: false, steamVrRunning: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewestEntryWinsWithinSameEvidenceClass(bool live)
    {
        var old = Headset("old", 100, live ? "connected" : "disconnected", live ? "operable" : "inoperable");
        var newer = Headset("new", 200, live ? "connected" : "disconnected", live ? "operable" : "inoperable");
        Assert.Equal("new", Select(old, newer)?.SerialNumber);
        Assert.Equal("new", Select(newer, old)?.SerialNumber);
    }

    [Fact]
    public void StaleConnectedInoperableHeadsetDoesNotEnableMetaWritesOverSteamVr()
    {
        var selected = Select(Headset("stale", 200, "connected", "inoperable", "primary"));
        Assert.False(LinkConnectionProbeService.LooksLikeStrongMetaSession(
            selected, metaHmd: false, audioLink: true, steamVrRunning: true));
        Assert.False(LinkConnectionProbeService.LooksLikeStrongMetaSession(
            selected, metaHmd: false, audioLink: true, steamVrRunning: false, virtualDesktopRunning: true));
    }

    [Fact]
    public void LiveMetaHeadsetWinsOverResidentVirtualDesktopProcess()
    {
        var selected = Select(Headset("live", 200, "connected", "operable", "primary"));

        Assert.True(LinkConnectionProbeService.LooksLikeStrongMetaSession(
            selected, metaHmd: false, audioLink: false, steamVrRunning: false, virtualDesktopRunning: true));
    }

    [Fact]
    public void NonHeadsetDevicesAreIgnoredAndEmptyCacheHasNoMetaEvidence()
    {
        var controller = Headset("controller", 999, "connected", "operable", type: "controller");
        Assert.Null(Select(controller));
        Assert.Null(Select());
        Assert.False(LinkConnectionProbeService.LooksLikeStrongMetaSession(
            null, metaHmd: false, audioLink: false, steamVrRunning: true));
    }
}
