using System.IO.Pipes;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
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
    public const string PipeName = "MetaQuestTrayTool.SessionHelper";
    private const string MutexName = @"Local\MetaQuestTrayTool.SessionHelper";

    public static bool IsHelperProcess(IEnumerable<string> args) =>
        args.Any(arg => string.Equals(arg, Switch, StringComparison.OrdinalIgnoreCase));

    public static void Attach(System.Windows.Application app)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            mutex.Dispose();
            app.Shutdown();
            return;
        }

        app.Exit += (_, _) =>
        {
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
        _ = Task.Run(() => ListenLoop(app, cts.Token), cts.Token);
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
                    app.Dispatcher.BeginInvoke(() => app.Shutdown());
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
}
