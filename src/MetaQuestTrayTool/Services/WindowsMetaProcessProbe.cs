using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Finds a process only when its main module path is exactly the installed Meta executable.
/// A process name such as Client is not enough.
/// </summary>
public sealed class WindowsMetaProcessProbe : IMetaProcessProbe
{
    public IReadOnlyList<IMetaProcess> FindExact(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return [];
        }

        var expected = Path.GetFullPath(fullPath);
        var name = Path.GetFileNameWithoutExtension(expected);
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var matches = new List<IMetaProcess>();
        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                var module = process.MainModule?.FileName;
                if (module is not null
                    && string.Equals(Path.GetFullPath(module), expected, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new WindowsMetaProcess(process));
                    continue;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The path could not be verified, so this process is not closed.
            }

            process.Dispose();
        }

        return matches;
    }

    private sealed class WindowsMetaProcess : IMetaProcess
    {
        private const uint WmClose = 0x0010;
        private readonly Process _process;

        public WindowsMetaProcess(Process process)
        {
            _process = process;
            Path = process.MainModule?.FileName ?? string.Empty;
        }

        public string Path { get; }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public void RequestClose()
        {
            try
            {
                _process.CloseMainWindow();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            EnumTopLevelWindows(CloseMatchingWindow, new IntPtr(_process.Id));
        }

        private static bool CloseMatchingWindow(IntPtr hwnd, IntPtr lParam)
        {
            _ = GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid == (uint)lParam.ToInt32())
            {
                PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
            }

            return true;
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            try
            {
                return _process.WaitForExit(timeout);
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        public void KillVerifiedProcess()
        {
            _process.Kill(entireProcessTree: false);
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "EnumWindows")]
    private static extern bool EnumTopLevelWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
