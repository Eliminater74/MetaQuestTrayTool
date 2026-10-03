using System.IO;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class AdbLifecycleTests
{
    [Fact]
    public void Startup_policy_does_not_execute_adb_or_kill_the_shared_server()
    {
        Assert.False(AdbSessionGate.StartupExecutesAdbCommands);
        Assert.False(AdbSessionGate.SessionEndKillsAdbServer);
        Assert.False(AdbSessionGate.AutomaticSweepDisconnectsOtherDevices);
        Assert.Contains("no active PCVR session", AdbSessionGate.IdleLogMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Idle_defaults_do_not_arm_the_watcher()
    {
        var gate = new AdbSessionGate();
        var settings = new HeadsetSettings();
        Assert.True(settings.ApplyWhenHeadsetConnects);
        Assert.False(settings.HeadsetOnlyWirelessAdb);
        Assert.False(gate.AllowsAutomaticAdb);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));
        Assert.False(AdbSessionGate.ShouldAttemptWirelessReconnect(gate.AllowsAutomaticAdb, settings));
    }

    [Fact]
    public void Apply_on_connect_alone_does_not_arm_without_a_live_session()
    {
        var settings = new HeadsetSettings
        {
            ApplyWhenHeadsetConnects = true,
            WirelessAutoReconnect = false,
            HeadsetOnlyWirelessAdb = true
        };
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(automaticSessionAllowed: false, settings));
    }

    [Fact]
    public void Headset_only_flag_does_not_keep_an_idle_or_live_watcher_alive()
    {
        var settings = new HeadsetSettings
        {
            ApplyWhenHeadsetConnects = false,
            WirelessAutoReconnect = false,
            HeadsetOnlyWirelessAdb = true
        };
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(false, settings));
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(true, settings));
    }

    [Fact]
    public void Wireless_auto_reconnect_does_not_retry_without_a_live_session()
    {
        var settings = new HeadsetSettings
        {
            ApplyWhenHeadsetConnects = false,
            WirelessAutoReconnect = true,
            WirelessHost = "192.168.1.40",
            WirelessPort = 5555
        };
        Assert.NotNull(settings.WirelessEndpoint);
        Assert.False(AdbSessionGate.ShouldAttemptWirelessReconnect(false, settings));
        Assert.True(AdbSessionGate.ShouldAttemptWirelessReconnect(true, settings));
    }

    [Theory]
    [InlineData(VrConnectionKind.MetaAirLink)]
    [InlineData(VrConnectionKind.MetaWiredLink)]
    public void Confirmed_meta_link_session_arms_automatic_adb(VrConnectionKind kind)
    {
        var gate = new AdbSessionGate();
        var changed = gate.NotifySession(Live(kind, metaStreaming: true));
        Assert.True(changed);
        Assert.True(gate.AllowsAutomaticAdb);
        Assert.Equal(AdbActivityMode.SessionActive, gate.Mode);
        Assert.True(HeadsetWatchService.ShouldArmAutomaticWatcher(
            gate.AllowsAutomaticAdb,
            new HeadsetSettings { ApplyWhenHeadsetConnects = true }));
    }

    [Fact]
    public void Air_link_auto_connect_without_a_stream_does_not_arm_adb()
    {
        var gate = new AdbSessionGate();
        gate.NotifySession(new VrConnectionStatus
        {
            Kind = VrConnectionKind.MetaAirLink,
            Summary = "Meta Wi-Fi auto-connect",
            SessionActive = true,
            MetaLinkStreaming = false,
            IsUsingAirLink = true
        });
        Assert.False(gate.AllowsAutomaticAdb);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, new HeadsetSettings()));
    }

    [Theory]
    [InlineData(VrConnectionKind.SteamLinkOrSteamVr)]
    [InlineData(VrConnectionKind.VirtualDesktop)]
    public void Supported_non_meta_sessions_follow_headset_adb_capability(VrConnectionKind kind)
    {
        var status = Live(kind, metaStreaming: false);
        var caps = VrSessionCapabilities.From(status);
        Assert.True(caps.SessionActive);
        Assert.True(caps.AllowsHeadsetAdb);
        Assert.False(caps.AllowsMetaLinkRegistry);

        var gate = new AdbSessionGate();
        gate.NotifySession(status);
        Assert.True(gate.AllowsAutomaticAdb);
        Assert.True(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, new HeadsetSettings()));
    }

    [Fact]
    public void Idle_capability_default_does_not_arm_adb()
    {
        var idle = new VrConnectionStatus
        {
            Kind = VrConnectionKind.Idle,
            Summary = "No PCVR session",
            SessionActive = false
        };
        var caps = VrSessionCapabilities.From(idle);
        Assert.True(caps.AllowsHeadsetAdb);
        Assert.False(caps.SessionActive);

        var gate = new AdbSessionGate();
        Assert.False(gate.NotifySession(idle));
        Assert.False(gate.AllowsAutomaticAdb);
        Assert.Equal(AdbActivityMode.Idle, gate.Mode);
    }

    [Fact]
    public void Session_end_stops_automatic_adb_and_reconnect_can_arm_it_again()
    {
        var gate = new AdbSessionGate();
        var settings = new HeadsetSettings
        {
            ApplyWhenHeadsetConnects = true,
            WirelessAutoReconnect = true,
            WirelessHost = "10.0.0.8"
        };
        gate.NotifySession(Live(VrConnectionKind.MetaAirLink, metaStreaming: true));
        var generation = gate.Generation;
        Assert.True(gate.IsCurrent(generation));

        Assert.True(gate.NotifyEnded());
        Assert.False(gate.IsCurrent(generation));
        Assert.False(gate.AllowsAutomaticAdb);
        Assert.Equal(AdbActivityMode.Idle, gate.Mode);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));
        Assert.False(AdbSessionGate.ShouldAttemptWirelessReconnect(gate.AllowsAutomaticAdb, settings));

        gate.NotifySession(Live(VrConnectionKind.MetaWiredLink, metaStreaming: true));
        Assert.True(gate.AllowsAutomaticAdb);
        Assert.NotEqual(generation, gate.Generation);
        Assert.True(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));
    }

    [Fact]
    public void Stale_generation_does_not_count_as_current_after_session_end()
    {
        var gate = new AdbSessionGate();
        gate.NotifySession(Live(VrConnectionKind.MetaAirLink, metaStreaming: true));
        var inFlight = gate.Generation;
        gate.NotifyEnded();
        Assert.False(gate.IsCurrent(inFlight));
        Assert.False(gate.NotifyEnded());
        Assert.False(gate.IsCurrent(inFlight));
    }

    [Fact]
    public void Pause_suppresses_automatic_adb_during_pcvr_and_resume_while_idle_does_not_arm()
    {
        var gate = new AdbSessionGate();
        gate.NotifySession(Live(VrConnectionKind.MetaAirLink, metaStreaming: true));
        var paused = new HeadsetSettings { ApplyWhenHeadsetConnects = true, AdbWatcherPaused = true };
        Assert.True(gate.AllowsAutomaticAdb);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, paused));

        gate.NotifyEnded();
        var resumed = new HeadsetSettings { ApplyWhenHeadsetConnects = true, AdbWatcherPaused = false };
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, resumed));
        Assert.False(AdbSessionGate.ShouldAttemptWirelessReconnect(gate.AllowsAutomaticAdb, resumed));
    }

    [Fact]
    public void Manual_action_while_idle_or_paused_does_not_arm_background_watching()
    {
        var gate = new AdbSessionGate();
        var settings = new HeadsetSettings { ApplyWhenHeadsetConnects = true, AdbWatcherPaused = true };
        gate.BeginManual();
        Assert.Equal(AdbActivityMode.ManualCheck, gate.Mode);
        Assert.False(gate.AllowsAutomaticAdb);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));

        gate.NotifySession(Live(VrConnectionKind.MetaAirLink, metaStreaming: true));
        Assert.True(gate.AllowsAutomaticAdb);
        Assert.Equal(AdbActivityMode.ManualCheck, gate.Mode);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));

        settings.AdbWatcherPaused = false;
        gate.EndManual();
        Assert.Equal(AdbActivityMode.SessionActive, gate.Mode);
        Assert.True(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));

        gate.NotifyEnded();
        gate.BeginManual();
        gate.EndManual();
        Assert.Equal(AdbActivityMode.Idle, gate.Mode);
        Assert.False(HeadsetWatchService.ShouldArmAutomaticWatcher(gate.AllowsAutomaticAdb, settings));
    }

    [Fact]
    public void Wireless_reconnect_after_session_end_is_not_current_work()
    {
        var gate = new AdbSessionGate();
        var settings = new HeadsetSettings
        {
            WirelessAutoReconnect = true,
            WirelessHost = "192.168.1.50",
            ApplyWhenHeadsetConnects = true
        };
        gate.NotifySession(Live(VrConnectionKind.MetaAirLink, metaStreaming: true));
        var generation = gate.Generation;
        Assert.True(AdbSessionGate.ShouldAttemptWirelessReconnect(gate.AllowsAutomaticAdb, settings));
        gate.NotifyEnded();
        Assert.False(gate.IsCurrent(generation));
        Assert.False(AdbSessionGate.ShouldAttemptWirelessReconnect(gate.AllowsAutomaticAdb, settings));
    }

    [Fact]
    public void Saved_headset_only_true_is_retired_and_sweep_does_not_touch_adb()
    {
        var settings = new HeadsetSettings { HeadsetOnlyWirelessAdb = true };
        Assert.True(HeadsetSettings.RetireExclusiveWirelessSweep(settings));
        Assert.False(settings.HeadsetOnlyWirelessAdb);
        Assert.True(settings.ExclusiveWirelessSweepRetired);
        Assert.False(HeadsetSettings.RetireExclusiveWirelessSweep(settings));

        var adb = new AdbService();
        Assert.Null(adb.SweepNonHeadsetWireless(new HeadsetSettings { HeadsetOnlyWirelessAdb = true }));
        Assert.Equal("ADB: Idle (on demand)", adb.DescribeCachedStatus());
        Assert.Throws<InvalidOperationException>(() => adb.DisconnectWireless());
        Assert.Equal(AdbActivityMode.Idle, adb.ActivityMode);
    }

    [Fact]
    public void Passive_status_labels_do_not_require_a_live_probe()
    {
        var adb = new AdbService();
        Assert.Equal("ADB: Idle (on demand)", adb.DescribeCachedStatus());
        Assert.Contains("PCVR connection", AdbSessionGate.HeadsetPageIdleText, StringComparison.Ordinal);
        adb.NoteSessionActive();
        Assert.Equal("ADB: Active for PCVR session", adb.DescribeCachedStatus());
        adb.NoteManualCheck();
        Assert.Equal("ADB: Manual check", adb.DescribeCachedStatus());
        adb.NoteManualCheckFinished(sessionActive: false);
        Assert.Equal("ADB: Idle (on demand)", adb.DescribeCachedStatus());
        Assert.Equal(AdbActivityMode.Idle, adb.ActivityMode);
    }

    [Fact]
    public void Startup_and_passive_ui_sources_do_not_execute_adb()
    {
        var app = ReadRepoFile("src/MetaQuestTrayTool/App.xaml.cs");
        var startup = Slice(app, "protected override void OnStartup", "_linkSessionWatcher.Start()");
        Assert.Contains("Adb.Refresh()", startup, StringComparison.Ordinal);
        Assert.Contains("AdbSessionGate.IdleLogMessage", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("Headset.ReadIdentity", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("Adb.DescribeStatus", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("kill-server", startup, StringComparison.Ordinal);

        var watch = ReadRepoFile("src/MetaQuestTrayTool/Services/HeadsetWatchService.cs");
        Assert.DoesNotContain("SweepNonHeadsetWireless", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("KillServerForUpdate", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kill-server\"", watch, StringComparison.Ordinal);

        var probe = ReadRepoFile("src/MetaQuestTrayTool/Services/LinkConnectionProbeService.cs");
        Assert.DoesNotContain("AdbService", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("adb.exe", probe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ListDevices", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("FindQuest", probe, StringComparison.Ordinal);

        var status = ReadRepoFile("src/MetaQuestTrayTool/Services/StatusDashboardService.cs");
        Assert.DoesNotContain("includeHeadset: true", status, StringComparison.Ordinal);
        Assert.Contains("includeHeadset: false", status, StringComparison.Ordinal);

        var info = ReadRepoFile("src/MetaQuestTrayTool/Views/Pages/InfoPage.xaml.cs");
        Assert.DoesNotContain("includeHeadset: true", info, StringComparison.Ordinal);

        var headsetPage = ReadRepoFile("src/MetaQuestTrayTool/Views/Pages/HeadsetPage.xaml.cs");
        Assert.Contains("ShowPassiveHeadsetStatus()", headsetPage, StringComparison.Ordinal);
        Assert.Equal(1, Count(headsetPage, "Headset.ReadIdentity"));

        var tray = ReadRepoFile("src/MetaQuestTrayTool/Tray/TrayIconHost.cs");
        Assert.DoesNotContain("DescribeStatus()", tray, StringComparison.Ordinal);
        Assert.DoesNotContain("includeHeadset: true", tray, StringComparison.Ordinal);
        Assert.Contains("DescribeCachedStatus()", tray, StringComparison.Ordinal);

        var connect = Slice(
            ReadRepoFile("src/MetaQuestTrayTool/Services/AdbService.cs"),
            "string ConnectWirelessHeadset",
            "string PairWireless");
        Assert.DoesNotContain("Sweep", connect, StringComparison.Ordinal);
        Assert.DoesNotContain("disconnect", connect, StringComparison.OrdinalIgnoreCase);
    }

    private static VrConnectionStatus Live(VrConnectionKind kind, bool metaStreaming) => new()
    {
        Kind = kind,
        Summary = kind.ToString(),
        SessionActive = true,
        MetaLinkStreaming = metaStreaming
    };

    private static string ReadRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        Assert.True(end > start, endMarker);
        return source[start..end];
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
