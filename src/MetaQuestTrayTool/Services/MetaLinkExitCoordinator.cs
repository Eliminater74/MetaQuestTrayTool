using System.IO;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Closes the verified Meta Horizon Link client and then stops OVRService.
/// Stopping the Windows service alone leaves Client.exe running. See GitHub issue #27.
/// </summary>
public sealed class MetaLinkExitCoordinator
{
    public static readonly TimeSpan DefaultGracefulWait = TimeSpan.FromSeconds(4);

    private int _started;

    public MetaLinkExitResult Run(
        MetaLinkExitRequest request,
        IMetaProcessProbe probe,
        Func<string> stopService,
        TimeSpan? gracefulWait = null)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return new MetaLinkExitResult(
                Applied: false,
                Duplicate: true,
                Summary: "Meta exit cleanup was already requested.",
                Steps: []);
        }

        if (!request.StopMetaOnExit)
        {
            return new MetaLinkExitResult(
                Applied: false,
                Duplicate: false,
                Summary: "Stop on exit is off. Meta Horizon Link and OVRService were left running.",
                Steps: []);
        }

        var wait = gracefulWait.GetValueOrDefault(DefaultGracefulWait);
        if (wait < TimeSpan.Zero)
        {
            wait = TimeSpan.Zero;
        }

        var notes = new List<string>();
        var steps = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ClientExecutable))
        {
            notes.Add("Meta Horizon Link client was not found.");
        }
        else
        {
            steps.Add("client");
            notes.Add(CloseVerified(probe, request.ClientExecutable, wait, forceAfterTimeout: true));
        }

        steps.Add("service");
        string serviceResult;
        try
        {
            serviceResult = stopService();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException or IOException)
        {
            serviceResult = "Could not stop OVRService: " + ex.GetType().Name;
        }

        notes.Add(serviceResult);
        var serviceFailed = IsServiceFailure(serviceResult);
        if (serviceFailed)
        {
            notes.Add("The runtime server was left running because OVRService did not stop.");
        }
        else if (string.IsNullOrWhiteSpace(request.RuntimeServerExecutable))
        {
            notes.Add("OVRServer_x64 path was not available.");
        }
        else
        {
            steps.Add("server");
            notes.Add(CloseVerified(probe, request.RuntimeServerExecutable, wait, forceAfterTimeout: true));
        }

        return new MetaLinkExitResult(true, false, string.Join(" ", notes), steps);
    }

    private static string CloseVerified(IMetaProcessProbe probe, string fullPath, TimeSpan wait, bool forceAfterTimeout)
    {
        IReadOnlyList<IMetaProcess> matches;
        try
        {
            matches = probe.FindExact(fullPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return "Could not inspect " + Path.GetFileName(fullPath) + ": " + ex.GetType().Name;
        }

        if (matches.Count == 0)
        {
            return Path.GetFileName(fullPath) + " was not running.";
        }

        var closed = 0;
        var forced = 0;
        var failed = 0;
        foreach (var process in matches)
        {
            try
            {
                process.RequestClose();
                if (process.HasExited || process.WaitForExit(wait))
                {
                    closed++;
                    continue;
                }

                if (!forceAfterTimeout)
                {
                    failed++;
                    continue;
                }

                process.KillVerifiedProcess();
                if (process.HasExited || process.WaitForExit(TimeSpan.FromSeconds(2)))
                {
                    forced++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                failed++;
            }
        }

        return Path.GetFileName(fullPath)
               + " close requested for "
               + matches.Count
               + " verified process"
               + (matches.Count == 1 ? "" : "es")
               + ". Closed " + closed
               + ", forced " + forced
               + ", still running " + failed + ".";
    }

    internal static bool IsServiceFailure(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return true;
        }

        return summary.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
               || summary.Contains("Could not", StringComparison.OrdinalIgnoreCase)
               || summary.Contains("timed out", StringComparison.OrdinalIgnoreCase)
               || summary.Contains("was not found", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record MetaLinkExitRequest
{
    public bool StopMetaOnExit { get; init; }
    public string? ClientExecutable { get; init; }
    public string? RuntimeServerExecutable { get; init; }
}

public sealed record MetaLinkExitResult(
    bool Applied,
    bool Duplicate,
    string Summary,
    IReadOnlyList<string> Steps);

public interface IMetaProcessProbe
{
    IReadOnlyList<IMetaProcess> FindExact(string fullPath);
}

public interface IMetaProcess
{
    string Path { get; }
    void RequestClose();
    bool HasExited { get; }
    bool WaitForExit(TimeSpan timeout);
    void KillVerifiedProcess();
}

public static class UserExitSource
{
    public const string Hotkey = "hotkey";
    public const string Voice = "voice";
    public const string TrayMenu = "tray menu";
    public const string MainWindow = "main window";
    public const string QuickControl = "quick control";
    public const string Updater = "updater";
    public const string WindowsSession = "windows session";
}

/// <summary>One user-exit request wins. Later tray, hotkey, and flyout requests do not start a second shutdown.</summary>
public sealed class UserExitRouter
{
    private int _entered;
    private readonly List<string> _attempts = [];

    public IReadOnlyList<string> Attempts => _attempts;

    public bool TryBegin(string source, Action beginShutdown)
    {
        _attempts.Add(source);
        if (Interlocked.Exchange(ref _entered, 1) != 0)
        {
            return false;
        }

        beginShutdown();
        return true;
    }
}
