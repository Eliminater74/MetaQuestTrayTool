using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Elevated tray client for <see cref="SessionHelperHost"/>. Starts any user-facing
/// program (SteamVR, Steam games, Horizon Link, Debug Tool, browsers, folders) as the
/// logged-on user. Steam-family launches skip the helper when <c>steam.exe</c> is already
/// elevated so SteamVR matches that Steam instance.
/// </summary>
public static class SessionHelperClient
{
    private static readonly TimeSpan PipeConnectTimeout = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan PipeReplyTimeout = TimeSpan.FromMilliseconds(800);

    public static bool IsSteamRunningElevated() =>
        UnelevatedProcessLauncher.TryGetNamedProcessElevated("steam", out var elevated) && elevated;

    /// <summary>
    /// Use the helper when the tray is elevated, except SteamVR / steam:// when Steam
    /// itself is already running as Administrator.
    /// </summary>
    public static bool ShouldUseHelper(bool steamFamily)
    {
        if (!UnelevatedProcessLauncher.IsCurrentProcessElevated())
        {
            return false;
        }

        return !(steamFamily && IsSteamRunningElevated());
    }

    public static bool TryPing(out string detail) => TrySend("PING", out detail);

    public static string EnsureRunning()
    {
        if (TryPing(out _))
        {
            return "Session helper already running (normal user).";
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "Could not start session helper — missing process path.";
        }

        var drop = UnelevatedProcessLauncher.IsCurrentProcessElevated();
        var helperArguments = SessionHelperHost.BuildArgumentsForParent(Environment.ProcessId);
        var started = drop
            ? UnelevatedProcessLauncher.TryStartHiddenUnelevated(
                exe,
                helperArguments,
                Path.GetDirectoryName(exe),
                out var startDetail)
            : UnelevatedProcessLauncher.TryStart(
                exe,
                helperArguments,
                Path.GetDirectoryName(exe),
                dropElevation: false,
                out startDetail);
        if (!started)
        {
            return "Could not start session helper: " + startDetail;
        }

        for (var i = 0; i < 15; i++)
        {
            Thread.Sleep(200);
            if (TryPing(out _))
            {
                return "Started session helper (normal user, " + startDetail + ").";
            }
        }

        TryPing(out var ping);
        return "Session helper started but did not answer (" + startDetail + " / " + ping + ").";
    }

    public static bool TryLaunchSteamVr(out string detail) =>
        TryLaunchUri("steam://run/250820", out detail);

    public static bool TryLaunchUri(string uri, out string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        return TryLaunch(
            LooksLikeSteamFamily(uri: uri),
            helperCommand: "URI64\t" + Encode(uri),
            fileName: uri,
            arguments: null,
            workingDirectory: null,
            out detail);
    }

    public static bool TryLaunchExe(
        string path,
        string? arguments,
        string? workingDirectory,
        out string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return TryLaunch(
            LooksLikeSteamFamily(exePath: path),
            helperCommand: "EXE64\t"
                           + Encode(path) + "\t"
                           + Encode(arguments) + "\t"
                           + Encode(workingDirectory),
            fileName: path,
            arguments: arguments,
            workingDirectory: workingDirectory,
            out detail);
    }

    public static void RequestQuit()
    {
        var pids = GetKnownHelperProcessIds();

        TrySend("QUIT", out _);
        if (pids.Count == 0)
        {
            return;
        }

        foreach (var pid in pids)
        {
            if (WaitForHelperExit(pid, TimeSpan.FromSeconds(3)))
            {
                continue;
            }

            // The helper may be stuck while the tray is exiting. Only terminate a
            // helper PID that either identified itself over the protected pipe, was
            // recorded by helper startup, or is a same-exe child of this tray process.
            TryTerminateHelper(pid);
        }
    }

    public static string RepairKnownHelpers()
    {
        var pids = GetKnownHelperProcessIds();
        var sentQuit = TrySend("QUIT", out var quitDetail);
        if (pids.Count == 0)
        {
            TryDeleteStaleHelperState(Array.Empty<int>());
            return sentQuit
                ? "Session helper accepted quit, but no helper PID was available to verify."
                : "No trusted session helper process was found. Pipe detail: " + quitDetail;
        }

        var results = new List<string>
        {
            sentQuit
                ? "Sent quit request to session helper."
                : "Could not send helper quit request: " + quitDetail
        };

        foreach (var pid in pids.Order())
        {
            if (WaitForHelperExit(pid, TimeSpan.FromSeconds(3)))
            {
                results.Add($"Helper PID {pid} exited.");
                continue;
            }

            results.Add(TryTerminateHelper(pid)
                ? $"Helper PID {pid} was still running and was terminated."
                : $"Helper PID {pid} did not exit and could not be terminated safely.");
        }

        TryDeleteStaleHelperState(pids);
        return string.Join(Environment.NewLine, results);
    }

    public static string DescribeHelperDiagnostics()
    {
        var lines = new List<string>
        {
            $"Current process PID: {Environment.ProcessId}",
            $"Current exe: {Environment.ProcessPath ?? "unknown"}"
        };

        if (TryGetHelperProcessId(out var pipePid))
        {
            lines.Add($"Helper pipe PID: {pipePid}");
        }
        else
        {
            lines.Add("Helper pipe PID: not responding");
        }

        if (SessionHelperHost.TryReadHelperState(out var state))
        {
            var trusted = IsTrustedRecordedHelperState(
                state,
                Environment.ProcessId,
                Environment.ProcessPath,
                IsProcessRunning);
            lines.Add(
                $"Recorded helper: pid={state.ProcessId}; parent={state.ParentProcessId}; "
                + $"running={IsProcessRunning(state.ProcessId)}; trusted={trusted}");
            lines.Add($"Recorded helper exe: {state.ExecutablePath ?? "unknown"}");
        }
        else
        {
            lines.Add("Recorded helper: none");
        }

        var children = GetSameExeChildProcessIds().Order().ToArray();
        lines.Add(children.Length == 0
            ? "Same-exe child helpers: none"
            : $"Same-exe child helpers: {string.Join(", ", children)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryLaunch(
        bool steamFamily,
        string helperCommand,
        string fileName,
        string? arguments,
        string? workingDirectory,
        out string detail)
    {
        if (!ShouldUseHelper(steamFamily))
        {
            var skipped = steamFamily && IsSteamRunningElevated()
                ? "helper skipped — Steam is already elevated"
                : "started in this process";
            var drop = false;
            var ok = fileName.Contains("://", StringComparison.Ordinal)
                ? UnelevatedProcessLauncher.TryStartUri(fileName, drop, out var launched)
                : UnelevatedProcessLauncher.TryStart(fileName, arguments, workingDirectory, drop, out launched);
            detail = ok ? skipped + " (" + launched + ")" : launched;
            return ok;
        }

        var helper = EnsureRunning();
        if (TrySend(helperCommand, out var helperDetail))
        {
            detail = "session helper (normal user). " + helper + " " + helperDetail;
            return true;
        }

        var dropFallback = UnelevatedProcessLauncher.IsCurrentProcessElevated();
        var fallbackOk = fileName.Contains("://", StringComparison.Ordinal)
            ? UnelevatedProcessLauncher.TryStartUri(fileName, dropFallback, out var fallback)
            : UnelevatedProcessLauncher.TryStart(fileName, arguments, workingDirectory, dropFallback, out fallback);
        detail = fallbackOk
            ? "helper send failed (" + helperDetail + "); started unelevated. " + fallback
            : "helper: " + helperDetail + "; direct: " + fallback;
        return fallbackOk;
    }

    internal static bool LooksLikeSteamFamily(string? uri = null, string? exePath = null)
    {
        if (!string.IsNullOrWhiteSpace(uri)
            && uri.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        var name = Path.GetFileName(exePath);
        return name.Equals("vrstartup.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("steamtours.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("vrserver.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("vrmonitor.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("vrdashboard.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("steam.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySend(string command, out string detail)
    {
        detail = "session helper not running";
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                SessionHelperHost.PipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            client.Connect((int)PipeConnectTimeout.TotalMilliseconds);
            TrySetPipeTimeouts(client);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            writer.WriteLine(command);
            var response = ReadLineWithTimeout(reader, PipeReplyTimeout, out var readDetail);
            if (string.IsNullOrWhiteSpace(response))
            {
                detail = readDetail;
                return false;
            }

            detail = response.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                ? response[2..].Trim()
                : response;
            return response.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static void TrySetPipeTimeouts(NamedPipeClientStream client)
    {
        try
        {
            client.ReadTimeout = (int)PipeReplyTimeout.TotalMilliseconds;
            client.WriteTimeout = (int)PipeReplyTimeout.TotalMilliseconds;
        }
        catch
        {
            // Some pipe configurations do not support stream timeouts.
        }
    }

    private static string? ReadLineWithTimeout(StreamReader reader, TimeSpan timeout, out string detail)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            detail = "empty helper reply";
            return reader.ReadLineAsync(cts.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            detail = "helper reply timed out";
            return null;
        }
        catch (TimeoutException)
        {
            detail = "helper reply timed out";
            return null;
        }
    }

    private static bool TryGetHelperProcessId(out int pid)
    {
        pid = 0;
        return TrySend("PID", out var detail)
            && int.TryParse(detail, out pid)
            && pid > 0
            && pid != Environment.ProcessId;
    }

    private static HashSet<int> GetKnownHelperProcessIds()
    {
        var pids = new HashSet<int>();
        if (TryGetHelperProcessId(out var pipePid))
        {
            pids.Add(pipePid);
        }

        if (TryGetRecordedHelperProcessId(out var recordedPid))
        {
            pids.Add(recordedPid);
        }

        foreach (var childPid in GetSameExeChildProcessIds())
        {
            pids.Add(childPid);
        }

        return pids;
    }

    private static bool TryGetRecordedHelperProcessId(out int pid)
    {
        pid = 0;
        if (!SessionHelperHost.TryReadHelperState(out var state)
            || !IsTrustedRecordedHelperState(
                state,
                Environment.ProcessId,
                Environment.ProcessPath,
                IsProcessRunning))
        {
            return false;
        }

        pid = state.ProcessId;
        return true;
    }

    internal static bool IsTrustedRecordedHelperState(
        SessionHelperHost.HelperState state,
        int currentPid,
        string? currentProcessPath,
        Func<int, bool> isProcessRunning)
    {
        if (state.ProcessId <= 0
            || state.ProcessId == currentPid
            || string.IsNullOrWhiteSpace(state.ExecutablePath)
            || string.IsNullOrWhiteSpace(currentProcessPath))
        {
            return false;
        }

        try
        {
            if (!string.Equals(
                    Path.GetFullPath(state.ExecutablePath),
                    Path.GetFullPath(currentProcessPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return state.ParentProcessId == currentPid
               || (state.ParentProcessId > 0 && !isProcessRunning(state.ParentProcessId));
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitForHelperExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!IsOwnedHelper(process))
            {
                return true;
            }

            return process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryTerminateHelper(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!IsOwnedHelper(process) || process.HasExited)
            {
                return false;
            }

            process.Kill(entireProcessTree: false);
            process.WaitForExit(3000);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            // The helper may have exited between the PID query and cleanup.
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteStaleHelperState(IReadOnlyCollection<int> candidatePids)
    {
        try
        {
            if (!SessionHelperHost.TryReadHelperState(out var state))
            {
                return;
            }

            var candidateMatch = candidatePids.Count == 0 || candidatePids.Contains(state.ProcessId);
            if (candidateMatch && !IsProcessRunning(state.ProcessId))
            {
                File.Delete(SessionHelperHost.HelperStateFile);
            }
        }
        catch
        {
            // Best effort diagnostic cleanup.
        }
    }

    private static bool IsOwnedHelper(Process process)
    {
        if (process.Id == Environment.ProcessId
            || string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                Path.GetFullPath(Environment.ProcessPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<int> GetSameExeChildProcessIds()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            yield break;
        }

        var processName = Path.GetFileNameWithoutExtension(exePath);
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            yield break;
        }

        try
        {
            foreach (var process in processes)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                if (TryGetParentProcessId(process.Id, out var parentPid)
                    && parentPid == Environment.ProcessId
                    && IsOwnedHelper(process))
                {
                    yield return process.Id;
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool TryGetParentProcessId(int pid, out int parentPid)
    {
        parentPid = 0;
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snapshot == InvalidHandleValue)
        {
            return false;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                dwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };
            if (!Process32First(snapshot, ref entry))
            {
                return false;
            }

            do
            {
                if (entry.th32ProcessID != pid)
                {
                    continue;
                }

                parentPid = (int)entry.th32ParentProcessID;
                return parentPid > 0;
            }
            while (Process32Next(snapshot, ref entry));

            return false;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private static string Encode(string? value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private const uint Th32csSnapprocess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
