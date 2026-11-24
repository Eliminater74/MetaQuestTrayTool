using System.Diagnostics;
using System.IO;

namespace MetaQuestTrayTool.Services;

public static class OverlayProcessHost
{
    private static readonly object Gate = new();
    private static Process? _process;

    public static ProcessStartInfo CreateStartInfo(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        return new ProcessStartInfo(executable)
        {
            Arguments = string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
    }

    public static string? FindExecutable()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "overlay", "MetaQuestTrayTool.Overlay.exe");
        if (File.Exists(beside))
        {
            return beside;
        }

        var flat = Path.Combine(AppContext.BaseDirectory, "MetaQuestTrayTool.Overlay.exe");
        return File.Exists(flat) ? flat : null;
    }

    public static string TryStart(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
        {
            return "Overlay companion was not started because the pipe token was missing. OpenVR was not loaded in the tray.";
        }

        var executable = FindExecutable();
        if (executable is null)
        {
            return "Overlay companion executable was not found. The tray process did not load OpenVR.";
        }

        lock (Gate)
        {
            StopHoldingLock();
            try
            {
                var process = Process.Start(CreateStartInfo(executable));
                if (process is null)
                {
                    return "Overlay companion did not start. The tray is still running and did not load OpenVR.";
                }

                process.StandardInput.WriteLine(token);
                process.StandardInput.Flush();
                if (process.WaitForExit(500))
                {
                    var error = process.StandardError.ReadToEnd();
                    process.Dispose();
                    return "Overlay companion exited on its own. The tray is still running. " + error.Trim();
                }

                _process = process;
                return "Overlay companion process is running. OpenVR is loaded only in that process. Physical SteamVR acceptance has not been performed.";
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return "Overlay companion failed to start (" + ex.GetType().Name + "). The tray is still running and did not load OpenVR.";
            }
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            StopHoldingLock();
        }
    }

    private static void StopHoldingLock()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("quit");
                process.StandardInput.Flush();
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1000);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
