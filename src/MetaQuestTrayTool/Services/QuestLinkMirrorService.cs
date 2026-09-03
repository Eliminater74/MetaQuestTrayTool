using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed record QuestLinkMirrorScreenshotResult(
    string FilePath,
    string Transport,
    long Bytes)
{
    public string Summary => $"Saved Quest Link mirror screenshot ({Transport}, {FormatBytes(Bytes)}) to {FilePath}.";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return (bytes / 1024d / 1024d).ToString("0.0 MB", CultureInfo.InvariantCulture);
        }

        if (bytes >= 1024)
        {
            return (bytes / 1024d).ToString("0.0 KB", CultureInfo.InvariantCulture);
        }

        return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
    }
}

public sealed class QuestLinkMirrorService
{
    private const int SwRestore = 9;
    private readonly App _app;

    public QuestLinkMirrorService(App app)
    {
        _app = app;
    }

    public QuestLinkMirrorScreenshotResult CaptureScreenshot()
    {
        var status = _app.LinkConnection.Probe();
        if (!IsMirrorCaptureAvailable(status))
        {
            throw new InvalidOperationException(BuildUnavailableMessage(status));
        }

        _app.Oculus.Refresh();
        var mirrorPath = _app.Oculus.MirrorPath;
        if (string.IsNullOrWhiteSpace(mirrorPath) || !File.Exists(mirrorPath))
        {
            throw new InvalidOperationException(
                "OculusMirror.exe was not found. Install or repair Meta Quest Link, then try again.");
        }

        var resolvedMirrorPath = mirrorPath;
        var process = GetOrStartMirror(resolvedMirrorPath, out var launched);
        try
        {
            var hwnd = WaitForMirrorWindow(process, TimeSpan.FromSeconds(10));
            var outputPath = CreateScreenshotPath(DateTimeOffset.Now, status.Kind);
            CaptureClientArea(hwnd, outputPath);
            return new QuestLinkMirrorScreenshotResult(
                outputPath,
                DescribeTransport(status.Kind),
                new FileInfo(outputPath).Length);
        }
        finally
        {
            if (launched)
            {
                CloseLaunchedMirror(process);
            }

            process.Dispose();
        }
    }

    internal static bool IsMirrorCaptureAvailable(VrConnectionStatus status) =>
        status is { SessionActive: true, MetaLinkStreaming: true }
        && status.Kind is VrConnectionKind.MetaAirLink
            or VrConnectionKind.MetaWiredLink
            or VrConnectionKind.MetaLinkUnknownTransport;

    internal static string BuildScreenshotFileName(
        DateTimeOffset capturedAt,
        VrConnectionKind kind,
        int? duplicateIndex = null)
    {
        var timestamp = capturedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = duplicateIndex is null ? string.Empty : "-" + duplicateIndex.Value.ToString(CultureInfo.InvariantCulture);
        return $"QuestLinkMirror-{timestamp}-{BuildTransportToken(kind)}{suffix}.png";
    }

    private static string BuildUnavailableMessage(VrConnectionStatus status)
    {
        if (status.Kind is VrConnectionKind.SteamLinkOrSteamVr or VrConnectionKind.VirtualDesktop)
        {
            return "Quest Link mirror screenshot needs Meta Quest Link / Air Link. This session is "
                   + $"{status.Summary}; use the ADB headset screenshot instead.";
        }

        if (status.SessionActive && !status.MetaLinkStreaming)
        {
            return "Quest Link mirror screenshot needs an active Link stream. Meta status is "
                   + $"{status.Describe()}.";
        }

        return "Quest Link mirror screenshot needs an active Meta Quest Link / Air Link session.";
    }

    private static Process GetOrStartMirror(string mirrorPath, out bool launched)
    {
        var existing = FindExistingMirror(mirrorPath);
        if (existing is not null)
        {
            launched = false;
            return existing;
        }

        var psi = new ProcessStartInfo
        {
            FileName = mirrorPath,
            Arguments = "--Size 1280 720",
            WorkingDirectory = Path.GetDirectoryName(mirrorPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        launched = true;
        return Process.Start(psi)
               ?? throw new InvalidOperationException("OculusMirror.exe did not start.");
    }

    private static Process? FindExistingMirror(string mirrorPath)
    {
        foreach (var process in Process.GetProcessesByName("OculusMirror"))
        {
            try
            {
                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // Access denied: fall back to process name below.
                }

                if (path is null || string.Equals(path, mirrorPath, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static IntPtr WaitForMirrorWindow(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"OculusMirror.exe exited before a mirror window was ready (code {process.ExitCode}).");
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException("OculusMirror.exe did not show a mirror window in time.");
    }

    private static void CaptureClientArea(IntPtr hwnd, string outputPath)
    {
        ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        Thread.Sleep(500);

        if (!GetClientRect(hwnd, out var rect))
        {
            throw new InvalidOperationException("Could not read the Oculus Mirror window size.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Oculus Mirror window has no drawable area.");
        }

        var topLeft = new NativePoint { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref topLeft))
        {
            throw new InvalidOperationException("Could not locate the Oculus Mirror window on screen.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppPaths.ScreenshotsDirectory);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                topLeft.X,
                topLeft.Y,
                0,
                0,
                new Size(width, height),
                CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(outputPath, ImageFormat.Png);
        if (!AdbService.IsValidPngFile(outputPath))
        {
            TryDeleteFile(outputPath);
            throw new InvalidOperationException("Quest Link mirror screenshot did not produce a valid PNG.");
        }
    }

    private static string CreateScreenshotPath(DateTimeOffset capturedAt, VrConnectionKind kind)
    {
        Directory.CreateDirectory(AppPaths.ScreenshotsDirectory);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var fileName = BuildScreenshotFileName(capturedAt, kind, attempt == 0 ? null : attempt + 1);
            var path = Path.Combine(AppPaths.ScreenshotsDirectory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(
            AppPaths.ScreenshotsDirectory,
            BuildScreenshotFileName(capturedAt, kind)
                .Replace(".png", "-" + Guid.NewGuid().ToString("N")[..8] + ".png", StringComparison.Ordinal));
    }

    private static string DescribeTransport(VrConnectionKind kind) => kind switch
    {
        VrConnectionKind.MetaAirLink => "Air Link mirror",
        VrConnectionKind.MetaWiredLink => "wired Link mirror",
        VrConnectionKind.MetaLinkUnknownTransport => "Meta Link mirror",
        _ => "Quest Link mirror"
    };

    private static string BuildTransportToken(VrConnectionKind kind) => kind switch
    {
        VrConnectionKind.MetaAirLink => "Air-Link",
        VrConnectionKind.MetaWiredLink => "Wired-Link",
        VrConnectionKind.MetaLinkUnknownTransport => "Meta-Link",
        _ => "Quest-Link"
    };

    private static void CloseLaunchedMirror(Process process)
    {
        try
        {
            process.Refresh();
            if (process.HasExited)
            {
                return;
            }

            if (process.CloseMainWindow() && process.WaitForExit(2_000))
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort cleanup for the mirror process this capture launched.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
