using System.IO.Pipes;
using System.IO;
using System.Text;

namespace MetaQuestTrayTool.Services;

/// <summary>Elevated tray client for <see cref="SessionHelperHost"/>.</summary>
public static class SessionHelperClient
{
    public static bool TryPing(out string detail)
    {
        return TrySend("PING", out detail);
    }

    public static string EnsureRunning()
    {
        if (TryPing(out var ping) && ping.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
        {
            return "Session helper already running (normal user).";
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "Could not start session helper — missing process path.";
        }

        var drop = UnelevatedProcessLauncher.IsCurrentProcessElevated();
        if (!UnelevatedProcessLauncher.TryStart(
                exe,
                SessionHelperHost.Switch,
                Path.GetDirectoryName(exe),
                drop,
                out var startDetail))
        {
            return "Could not start session helper: " + startDetail;
        }

        for (var i = 0; i < 15; i++)
        {
            Thread.Sleep(200);
            if (TryPing(out ping) && ping.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            {
                return "Started session helper (normal user, " + startDetail + ").";
            }
        }

        return "Session helper started but did not answer (" + startDetail + " / " + ping + ").";
    }

    public static bool TryStartSteamVr(out string detail)
    {
        return TrySend("STEAMVR", out detail);
    }

    public static bool TryStartUri(string uri, out string detail)
    {
        return TrySend("URI\t" + uri, out detail);
    }

    public static bool TryStartExe(string path, string? arguments, string? workingDirectory, out string detail)
    {
        return TrySend(
            "EXE\t" + path + "\t" + (arguments ?? string.Empty) + "\t" + (workingDirectory ?? string.Empty),
            out detail);
    }

    public static void RequestQuit()
    {
        TrySend("QUIT", out _);
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
}
