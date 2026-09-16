using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class Issue12FreezePathTests
{
    [Theory]
    [InlineData(null, "1WMHH123", true)]
    [InlineData("1WMHH123", "1WMHH123", false)]
    [InlineData("1WMHH123", "1wmhh123", false)]
    [InlineData("1WMHH123", "192.168.1.40:5555", true)]
    [InlineData("192.168.1.40:5555", "1WMHH123", true)]
    [InlineData("192.168.1.40:5555", "192.168.1.41:5555", true)]
    public void UsbAndWirelessAdbSerialsAreDistinctConnections(string? last, string next, bool expected) =>
        Assert.Equal(expected, HeadsetWatchService.IsNewAdbConnection(last, next));

    [Fact]
    public void AdbConnectReappliesGlobalsUnlessAProfileIsActive()
    {
        Assert.True(HeadsetWatchService.ShouldApplyGlobalBaselineOnAdbApply(
            gameProfileActive: false, applyGlobalWhenHeadsetConnects: true));
        Assert.False(HeadsetWatchService.ShouldApplyGlobalBaselineOnAdbApply(
            gameProfileActive: true, applyGlobalWhenHeadsetConnects: true));
        Assert.False(HeadsetWatchService.ShouldApplyGlobalBaselineOnAdbApply(
            gameProfileActive: false, applyGlobalWhenHeadsetConnects: false));
    }

    [Fact]
    public void LiveWiredFingerprintIncludesDeviceCacheState()
    {
        var connected = LiveWired("connected");
        var disconnected = LiveWired("disconnected");
        Assert.NotEqual(
            LinkSessionWatchService.BuildFingerprint(connected),
            LinkSessionWatchService.BuildFingerprint(disconnected));
        Assert.StartsWith("active:", LinkSessionWatchService.BuildFingerprint(connected), StringComparison.Ordinal);
        Assert.Contains(":connected:", LinkSessionWatchService.BuildFingerprint(connected));
    }

    [Fact]
    public void DeviceCacheFlickerWhileStreamingIsStillALiveSession()
    {
        var flickered = LiveWired("disconnected");
        Assert.True(LinkSessionWatchService.IsLivePcvrSession(flickered));
        Assert.True(flickered.MetaLinkStreaming);
    }

    [Fact]
    public void MetaSessionWithoutStreamingIsNotLivePcvr()
    {
        var status = new VrConnectionStatus
        {
            Kind = VrConnectionKind.MetaWiredLink,
            Summary = "Meta wired Link — auto-connect / not streaming",
            SessionActive = true,
            MetaLinkStreaming = false,
            HeadsetSerial = "ABC",
            DeviceCacheConnectionState = "connected",
            IsUsingAirLink = false
        };

        Assert.False(LinkSessionWatchService.IsLivePcvrSession(status));
        Assert.StartsWith("broken:", LinkSessionWatchService.BuildFingerprint(status), StringComparison.Ordinal);
    }

    [Fact]
    public void FlightRecorderLogsFingerprintAndMutationChangesOnly()
    {
        var recorder = new SessionFlightRecorder(log: null, filePath: null);
        var live = LiveWired("connected");
        recorder.ObserveLinkCore(live, "test");
        recorder.ObserveLinkCore(live, "test");
        recorder.ObserveLinkCore(LiveWired("disconnected"), "test");
        recorder.Record(SessionTraceKind.Mutation, "link-registry", "WRITE", "bitrate=960", "ApplyGlobalBaseline");

        var events = recorder.Snapshot();
        Assert.Equal(3, events.Count);
        Assert.Equal(SessionTraceKind.State, events[0].Kind);
        Assert.Equal("initial", events[0].Action);
        Assert.Equal("change", events[1].Action);
        Assert.Equal(SessionTraceKind.Mutation, events[2].Kind);
        Assert.Contains("bitrate=960", events[2].Detail);
        Assert.Contains("[TRACE]", SessionFlightRecorder.LogPrefix);
        Assert.Contains("MUTATION", events[2].ToLogLine());
        Assert.Contains("Z |", events[2].ToLogLine());
    }

    [Fact]
    public void FlightRecorderDoesNotTreatRepeatedAdbSerialAsReconnect()
    {
        var recorder = new SessionFlightRecorder(log: null, filePath: null);
        recorder.ObserveAdbCore("1WMHH123", wireless: false, "test");
        recorder.ObserveAdbCore("1WMHH123", wireless: false, "test");
        recorder.ObserveAdbCore("192.168.1.40:5555", wireless: true, "test");

        var events = recorder.Snapshot();
        Assert.Equal(2, events.Count);
        Assert.Equal("connect", events[0].Action);
        Assert.Equal("reconnect", events[1].Action);
        Assert.Contains("transport=wireless", events[1].Detail);
        Assert.Equal("usb-serial", SessionFlightRecorder.DescribeAdbSerial("1WMHH123"));
        Assert.Equal("wireless-endpoint", SessionFlightRecorder.DescribeAdbSerial("192.168.1.40:5555"));
    }

    [Fact]
    public void FlightRecorderDropsOldestEventsPastCapacity()
    {
        var recorder = new SessionFlightRecorder(log: null, filePath: null);
        for (var i = 0; i < SessionFlightRecorder.Capacity + 3; i++)
        {
            recorder.Record(SessionTraceKind.State, "test", "tick", i.ToString(), "capacity");
        }

        var events = recorder.Snapshot();
        Assert.Equal(SessionFlightRecorder.Capacity, events.Count);
        Assert.Equal("3", events[0].Detail);
        Assert.Equal((SessionFlightRecorder.Capacity + 2).ToString(), events[^1].Detail);
    }

    private static VrConnectionStatus LiveWired(string connectionState) => new()
    {
        Kind = VrConnectionKind.MetaWiredLink,
        Summary = "Meta wired Link",
        SessionActive = true,
        MetaLinkStreaming = true,
        HeadsetSerial = "ABC",
        DeviceCacheConnectionState = connectionState,
        IsUsingAirLink = false
    };
}
