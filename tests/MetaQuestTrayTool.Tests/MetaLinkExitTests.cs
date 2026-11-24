using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public sealed class MetaLinkExitTests
{
    private const string Client = @"C:\Program Files\Oculus\Support\oculus-client\Client.exe";
    private const string Server = @"C:\Program Files\Oculus\Support\oculus-runtime\OVRServer_x64.exe";
    private const string Steam = @"C:\Program Files\Steam\steamapps\common\SteamVR\bin\win64\vrserver.exe";
    private const string Adb = @"C:\platform-tools\adb.exe";

    [Fact]
    public void DisabledPreferenceLeavesMetaProcessesUntouched()
    {
        var probe = new FakeProbe();
        probe.Add(Client, closeSucceeds: false);
        var stops = 0;
        var result = new MetaLinkExitCoordinator().Run(Request(false), probe, () =>
        {
            stops++;
            return "OVRService is now Stopped.";
        });

        Assert.False(result.Applied);
        Assert.Empty(probe.Queries);
        Assert.Equal(0, stops);
        Assert.Contains("left running", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HotkeyAndTrayExitUseTheSameCleanupOrder()
    {
        var seen = RunEnabled("OVRService is now Stopped.", clientCloses: true, serverStillRunning: false);

        Assert.Equal(["client", "service", "server"], seen.Result.Steps);
        Assert.Equal([Client, Server], seen.Probe.Queries);
        Assert.DoesNotContain(Steam, seen.Probe.Queries);
        Assert.DoesNotContain(Adb, seen.Probe.Queries);
        Assert.Equal(1, seen.Client.CloseRequests);
        Assert.Equal(0, seen.Client.Kills);
        Assert.True(seen.ServiceStoppedBeforeServerLookup);
    }

    [Fact]
    public void ClientIsClosedWhenTheServiceIsAlreadyStopped()
    {
        var seen = RunEnabled("OVRService is already stopped.", clientCloses: true, serverStillRunning: false);

        Assert.Equal(1, seen.Client.CloseRequests);
        Assert.Contains("client", seen.Result.Steps);
        Assert.Contains("service", seen.Result.Steps);
    }

    [Fact]
    public void ClientIsClosedBeforeAServiceFailureAndTheServerIsNotKilled()
    {
        var seen = RunEnabled("Access denied while changing OVRService.", clientCloses: true, serverStillRunning: true);

        Assert.Equal(1, seen.Client.CloseRequests);
        Assert.Equal(0, seen.Server.Kills);
        Assert.DoesNotContain("server", seen.Result.Steps);
        Assert.Contains("left running", seen.Result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AClientThatIgnoresCloseIsForcedOnlyAfterTheTimeout()
    {
        var seen = RunEnabled("OVRService is now Stopped.", clientCloses: false, serverStillRunning: false);

        Assert.Equal(1, seen.Client.CloseRequests);
        Assert.Equal(1, seen.Client.Kills);
        Assert.True(seen.Client.KilledAfterClose);
    }

    [Fact]
    public void ARuntimeServerThatSurvivesTheServiceStopIsClosedByExactPath()
    {
        var seen = RunEnabled("OVRService is now Stopped.", clientCloses: true, serverStillRunning: true);

        Assert.Equal(1, seen.Server.CloseRequests);
        Assert.Equal(1, seen.Server.Kills);
        Assert.Equal([Client, Server], seen.Probe.Queries);
    }

    [Fact]
    public void DuplicateExitDoesNotStopTheServiceTwice()
    {
        var coordinator = new MetaLinkExitCoordinator();
        var probe = new FakeProbe();
        var stops = 0;
        var request = Request(true);
        var first = coordinator.Run(request, probe, () =>
        {
            stops++;
            return "OVRService is now Stopped.";
        });
        var second = coordinator.Run(request, probe, () =>
        {
            stops++;
            return "OVRService is now Stopped.";
        });

        Assert.True(first.Applied);
        Assert.True(second.Duplicate);
        Assert.Equal(1, stops);
    }

    [Fact]
    public void EveryUserExitSourceSharesOneShutdown()
    {
        var router = new UserExitRouter();
        var began = 0;
        Assert.True(router.TryBegin(UserExitSource.Hotkey, () => began++));
        Assert.False(router.TryBegin(UserExitSource.TrayMenu, () => began++));
        Assert.False(router.TryBegin(UserExitSource.QuickControl, () => began++));
        Assert.False(router.TryBegin(UserExitSource.MainWindow, () => began++));
        Assert.False(router.TryBegin(UserExitSource.Voice, () => began++));
        Assert.False(router.TryBegin(UserExitSource.Updater, () => began++));
        Assert.False(router.TryBegin(UserExitSource.WindowsSession, () => began++));
        Assert.Equal(1, began);
        Assert.Equal(
            [
                UserExitSource.Hotkey,
                UserExitSource.TrayMenu,
                UserExitSource.QuickControl,
                UserExitSource.MainWindow,
                UserExitSource.Voice,
                UserExitSource.Updater,
                UserExitSource.WindowsSession
            ],
            router.Attempts);
    }

    private static Seen RunEnabled(string serviceResult, bool clientCloses, bool serverStillRunning)
    {
        var probe = new FakeProbe();
        var client = probe.Add(Client, clientCloses);
        var server = probe.Add(Server, closeSucceeds: false, present: serverStillRunning);
        probe.Add(Steam, closeSucceeds: false);
        probe.Add(Adb, closeSucceeds: false);
        var serverLookedUpAfterStop = false;
        var result = new MetaLinkExitCoordinator().Run(Request(true), probe, () =>
        {
            serverLookedUpAfterStop = probe.Queries.Contains(Client) && !probe.Queries.Contains(Server);
            return serviceResult;
        });
        return new Seen(result, probe, client, server, serverLookedUpAfterStop || !result.Steps.Contains("server"));
    }

    private static MetaLinkExitRequest Request(bool enabled) => new()
    {
        StopMetaOnExit = enabled,
        ClientExecutable = Client,
        RuntimeServerExecutable = Server
    };

    private sealed record Seen(
        MetaLinkExitResult Result,
        FakeProbe Probe,
        FakeProcess Client,
        FakeProcess Server,
        bool ServiceStoppedBeforeServerLookup);

    private sealed class FakeProbe : IMetaProcessProbe
    {
        private readonly Dictionary<string, FakeProcess> _processes = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Queries { get; } = [];

        public FakeProcess Add(string path, bool closeSucceeds, bool present = true)
        {
            var process = new FakeProcess(path, closeSucceeds, present);
            _processes[path] = process;
            return process;
        }

        public IReadOnlyList<IMetaProcess> FindExact(string fullPath)
        {
            Queries.Add(fullPath);
            if (_processes.TryGetValue(fullPath, out var process) && process.Present && !process.HasExited)
            {
                return [process];
            }

            return [];
        }
    }

    private sealed class FakeProcess : IMetaProcess
    {
        private readonly bool _closeSucceeds;

        public FakeProcess(string path, bool closeSucceeds, bool present)
        {
            Path = path;
            _closeSucceeds = closeSucceeds;
            Present = present;
        }

        public string Path { get; }
        public bool Present { get; }
        public int CloseRequests { get; private set; }
        public int Kills { get; private set; }
        public bool KilledAfterClose { get; private set; }
        public bool HasExited { get; private set; }

        public void RequestClose()
        {
            CloseRequests++;
            if (_closeSucceeds)
            {
                HasExited = true;
            }
        }

        public bool WaitForExit(TimeSpan timeout) => HasExited;

        public void KillVerifiedProcess()
        {
            KilledAfterClose = CloseRequests > 0;
            Kills++;
            HasExited = true;
        }
    }
}
