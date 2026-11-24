using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace MetaQuestTrayTool.Services;

public static class OverlayProcessHost
{
    private static readonly object Gate = new();
    private static Process? _process;
    private static StreamWriter? _input;

    public static string LaunchPlan(bool trayElevated) => trayElevated ? "unelevated-stdin" : "inherited-stdin";

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
                if (LaunchPlan(UnelevatedProcessLauncher.IsCurrentProcessElevated()) == "unelevated-stdin")
                {
                    return StartUnelevated(executable, token);
                }

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
                _input = process.StandardInput;
                return "Overlay companion process is running at the tray's integrity. OpenVR is loaded only in that process. Physical SteamVR acceptance has not been performed.";
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
                try
                {
                    _input?.WriteLine("quit");
                    _input?.Flush();
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                }

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
            _input = null;
            process.Dispose();
        }
    }

    private static string StartUnelevated(string executable, string token)
    {
        var pipe = new AnonymousPipeServerStream(PipeDirection.Out, System.IO.HandleInheritability.Inheritable);
        try
        {
            var started = UnelevatedProcessLauncher.TryStartHiddenWithStandardInput(
                executable,
                Path.GetDirectoryName(executable),
                pipe.ClientSafePipeHandle,
                out var processId,
                out var detail);
            pipe.DisposeLocalCopyOfClientHandle();
            if (!started || processId <= 0)
            {
                return "Overlay companion was not started unelevated (" + detail + "). The elevated tray did not fall back to an elevated companion and did not load OpenVR.";
            }

            var process = Process.GetProcessById(processId);
            var writer = new StreamWriter(pipe) { AutoFlush = true };
            pipe = null;
            writer.WriteLine(token);
            writer.Flush();
            if (process.WaitForExit(500))
            {
                writer.Dispose();
                process.Dispose();
                return "Overlay companion exited on its own. The tray is still running and did not load OpenVR. " + detail;
            }

            _process = process;
            _input = writer;
            return "Overlay companion process is running unelevated. OpenVR is loaded only in that process. Physical SteamVR acceptance has not been performed. " + detail;
        }
        finally
        {
            pipe?.Dispose();
        }
    }
}
