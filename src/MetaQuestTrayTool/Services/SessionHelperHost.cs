using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Same-exe helper that stays at the logged-on user's integrity (not Administrator).
/// The elevated tray talks to it over a named pipe to start Steam / SteamVR / games / any other user program.
/// Steam-family launches can skip this helper when steam.exe is already elevated.
/// A Windows Service cannot do this — Session 0 cannot launch the user's SteamVR session.
/// </summary>
public static class SessionHelperHost
{
    public const string Switch = "--session-helper";
    internal const string ParentPidSwitch = "--session-helper-parent-pid";
    public const string PipeName = "MetaQuestTrayTool.SessionHelper";
    private const string MutexName = @"Local\MetaQuestTrayTool.SessionHelper";
    internal static string HelperStateFile { get; } =
        Path.Combine(AppPaths.AppDataDirectory, "session-helper.json");

    public static bool IsHelperProcess(IEnumerable<string> args) =>
        args.Any(arg => string.Equals(arg, Switch, StringComparison.OrdinalIgnoreCase));

    internal static string BuildArgumentsForParent(int parentPid) =>
        $"{Switch} {ParentPidSwitch} {parentPid}";

    public static void Attach(System.Windows.Application app, IEnumerable<string> args)
    {
        var parentPid = ParseParentProcessId(args);
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            mutex.Dispose();
            app.Shutdown();
            return;
        }

        TryWriteHelperState(parentPid);
        app.Exit += (_, _) =>
        {
            TryDeleteHelperStateForCurrentProcess();
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // not owned
            }

            mutex.Dispose();
        };

        var cts = new CancellationTokenSource();
        app.Exit += (_, _) => cts.Cancel();
        if (parentPid > 0 && parentPid != Environment.ProcessId)
        {
            _ = Task.Run(() => WatchParent(app, parentPid, cts.Token), cts.Token);
        }

        _ = Task.Run(() => ListenLoop(app, cts.Token), cts.Token);
    }

    internal static int ParseParentProcessId(IEnumerable<string> args)
    {
        var values = args.ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            var arg = values[i];
            if (string.Equals(arg, ParentPidSwitch, StringComparison.OrdinalIgnoreCase)
                && i + 1 < values.Length
                && int.TryParse(values[i + 1], out var nextPid)
                && nextPid > 0)
            {
                return nextPid;
            }

            var prefix = ParentPidSwitch + "=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(arg[prefix.Length..], out var inlinePid)
                && inlinePid > 0)
            {
                return inlinePid;
            }
        }

        return 0;
    }

    private static void ListenLoop(System.Windows.Application app, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = CreateServer();
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteLine("OK shutting down");
                    RequestShutdown(app);
                    return;
                }

                if (line.Equals("PID", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteLine("OK " + Environment.ProcessId);
                    continue;
                }

                writer.WriteLine(Handle(line));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // next client
            }
        }
    }

    private static void WatchParent(System.Windows.Application app, int parentPid, CancellationToken token)
    {
        try
        {
            using var parent = Process.GetProcessById(parentPid);
            while (!token.IsCancellationRequested)
            {
                if (parent.WaitForExit(1000))
                {
                    break;
                }
            }
        }
        catch (ArgumentException)
        {
            // Parent has already exited.
        }
        catch (InvalidOperationException)
        {
            // Parent exited while the process handle was being prepared.
        }
        catch
        {
            // If parent monitoring is unavailable, keep the helper pipe alive.
            return;
        }

        if (!token.IsCancellationRequested)
        {
            RequestShutdown(app);
        }
    }

    private static void RequestShutdown(System.Windows.Application app)
    {
        try
        {
            app.Dispatcher.BeginInvoke(() => app.Shutdown());
        }
        catch
        {
            try
            {
                app.Shutdown();
            }
            catch
            {
                // Process teardown is already in flight.
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is not null)
        {
            security.AddAccessRule(new PipeAccessRule(
                identity.User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }

    private static string Handle(string line)
    {
        try
        {
            if (line.Equals("PING", StringComparison.OrdinalIgnoreCase))
            {
                return UnelevatedProcessLauncher.IsCurrentProcessElevated()
                    ? "ERR helper is elevated — SteamVR would inherit Administrator"
                    : "OK ready";
            }

            if (line.Equals("STEAMVR", StringComparison.OrdinalIgnoreCase))
            {
                return StartUri("steam://run/250820");
            }

            if (line.StartsWith("URI64\t", StringComparison.OrdinalIgnoreCase))
            {
                return StartUri(Decode(line[6..]));
            }

            if (line.StartsWith("URI\t", StringComparison.OrdinalIgnoreCase))
            {
                return StartUri(line[4..]);
            }

            if (line.StartsWith("EXE64\t", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('\t', StringSplitOptions.None);
                if (parts.Length != 4)
                {
                    return "ERR malformed exe request";
                }

                var path = Decode(parts[1]);
                var args = Decode(parts[2]);
                var cwd = Decode(parts[3]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return "ERR missing exe path";
                }

                if (!UnelevatedProcessLauncher.TryStart(path, args, cwd, dropElevation: false, out var detail))
                {
                    return "ERR " + detail;
                }

                return "OK " + detail;
            }

            if (line.StartsWith("EXE\t", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('\t');
                var path = parts.Length > 1 ? parts[1] : string.Empty;
                var args = parts.Length > 2 ? parts[2] : string.Empty;
                var cwd = parts.Length > 3 ? parts[3] : string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return "ERR missing exe path";
                }

                if (!UnelevatedProcessLauncher.TryStart(path, args, cwd, dropElevation: false, out var detail))
                {
                    return "ERR " + detail;
                }

                return "OK " + detail;
            }

            return "ERR unknown command";
        }
        catch (Exception ex)
        {
            return "ERR " + ex.Message;
        }
    }

    private static string StartUri(string uri)
    {
        if (!UnelevatedProcessLauncher.TryStartUri(uri, dropElevation: false, out var detail))
        {
            return "ERR " + detail;
        }

        return "OK " + detail;
    }

    private static string Decode(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    internal sealed record HelperState
    {
        public int ProcessId { get; init; }
        public int ParentProcessId { get; init; }
        public string? ExecutablePath { get; init; }
    }

    internal static bool TryReadHelperState(out HelperState state)
    {
        state = new HelperState();
        try
        {
            if (!File.Exists(HelperStateFile))
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<HelperState>(File.ReadAllText(HelperStateFile));
            if (parsed is null || parsed.ProcessId <= 0)
            {
                return false;
            }

            state = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryWriteHelperState(int parentPid)
    {
        try
        {
            AppPaths.EnsureAppDataDirectory();
            var state = new HelperState
            {
                ProcessId = Environment.ProcessId,
                ParentProcessId = parentPid,
                ExecutablePath = Environment.ProcessPath ?? string.Empty
            };
            File.WriteAllText(HelperStateFile, JsonSerializer.Serialize(state));
        }
        catch
        {
            // The helper pipe remains authoritative if the diagnostic state file cannot be written.
        }
    }

    private static void TryDeleteHelperStateForCurrentProcess()
    {
        try
        {
            if (TryReadHelperState(out var state) && state.ProcessId == Environment.ProcessId)
            {
                File.Delete(HelperStateFile);
            }
        }
        catch
        {
            // Best effort cleanup; future startups validate the PID before trusting this file.
        }
    }
}
