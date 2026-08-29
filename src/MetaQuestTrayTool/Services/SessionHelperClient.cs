using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
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
        var started = drop
            ? UnelevatedProcessLauncher.TryStartHiddenUnelevated(
                exe,
                SessionHelperHost.Switch,
                Path.GetDirectoryName(exe),
                out var startDetail)
            : UnelevatedProcessLauncher.TryStart(
                exe,
                SessionHelperHost.Switch,
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
        if (!TryGetHelperProcessId(out var pid))
        {
            TrySend("QUIT", out _);
            return;
        }

        TrySend("QUIT", out _);
        if (WaitForHelperExit(pid, TimeSpan.FromSeconds(3)))
        {
            return;
        }

        // The helper may be stuck while the tray is exiting. Only terminate the
        // process that identified itself over the protected helper pipe and whose
        // executable path matches this application.
        TryTerminateHelper(pid);
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
            client.Connect(800);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            writer.WriteLine(command);
            var response = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(response))
            {
                detail = "empty helper reply";
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

    private static bool TryGetHelperProcessId(out int pid)
    {
        pid = 0;
        return TrySend("PID", out var detail)
            && int.TryParse(detail, out pid)
            && pid > 0
            && pid != Environment.ProcessId;
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

    private static void TryTerminateHelper(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!IsOwnedHelper(process) || process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: false);
            process.WaitForExit(3000);
        }
        catch
        {
            // The helper may have exited between the PID query and cleanup.
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

    private static string Encode(string? value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
}
