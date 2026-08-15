using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Start a process at the logged-on user's integrity when the tray is elevated.
/// SteamVR is fine as Administrator when <b>Steam itself</b> is also elevated; mixed
/// High-IL vrstartup next to a normal Steam client is what produces a broken session.
/// </summary>
public static class UnelevatedProcessLauncher
{
    public static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// True when we should drop to the shell's medium token so the child matches
    /// the user's Steam (or starts Steam as the user). False when the tray is already
    /// not elevated, or when <paramref name="peerProcessName"/> is also elevated.
    /// </summary>
    public static bool ShouldDropElevationToMatch(string? peerProcessName)
    {
        if (!IsCurrentProcessElevated())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(peerProcessName))
        {
            return true;
        }

        return !TryIsNamedProcessElevated(peerProcessName, out var peerElevated) || !peerElevated;
    }

    public static bool TryStart(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool dropElevation,
        out string detail)
    {
        if (!dropElevation)
        {
            return TryStartDirect(fileName, arguments, workingDirectory, out detail);
        }

        if (TryStartWithShellToken(fileName, arguments, workingDirectory, hideWindow: false, out detail))
        {
            return true;
        }

        var tokenError = detail;
        if (TryStartViaExplorer(fileName, arguments, out detail))
        {
            detail += $" (explorer fallback; token: {tokenError})";
            return true;
        }

        detail = $"unelevated launch failed ({tokenError}); explorer: {detail}";
        return false;
    }

    public static bool TryStartUri(string uri, bool dropElevation, out string detail)
    {
        if (!dropElevation)
        {
            return TryStartDirect(uri, arguments: null, workingDirectory: null, out detail);
        }

        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        if (TryStartWithShellToken(
                cmd,
                $"/c start \"\" {uri}",
                workingDirectory: null,
                hideWindow: true,
                out detail))
        {
            return true;
        }

        var tokenError = detail;
        if (TryStartViaExplorer(uri, arguments: null, out detail))
        {
            detail += $" (explorer fallback; token: {tokenError})";
            return true;
        }

        detail = $"unelevated URI launch failed ({tokenError}); explorer: {detail}";
        return false;
    }

    /// <summary>
    /// True when a process with this name exists and its token is elevated.
    /// False if it is not running, or the token could not be read.
    /// </summary>
    public static bool TryGetNamedProcessElevated(string processName, out bool elevated) =>
        TryIsNamedProcessElevated(processName, out elevated);

    private static bool TryIsNamedProcessElevated(string processName, out bool elevated)
    {
        elevated = false;
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                if (TryIsProcessElevated(process, out elevated))
                {
                    return true;
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

        return false;
    }

    private static bool TryIsProcessElevated(Process process, out bool elevated)
    {
        elevated = false;
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)process.Id);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(handle, TokenQuery, out var token))
            {
                return false;
            }

            try
            {
                var elevation = new TokenElevation();
                if (!GetTokenInformation(
                        token,
                        TokenElevationType,
                        ref elevation,
                        Marshal.SizeOf<TokenElevation>(),
                        out _))
                {
                    return false;
                }

                elevated = elevation.TokenIsElevated != 0;
                return true;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool TryStartDirect(
        string fileName,
        string? arguments,
        string? workingDirectory,
        out string detail)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                UseShellExecute = true
            });
            detail = "started";
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static bool TryStartViaExplorer(string fileOrUri, string? arguments, out string detail)
    {
        try
        {
            var explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");
            var target = string.IsNullOrWhiteSpace(arguments)
                ? Quote(fileOrUri)
                : $"{Quote(fileOrUri)} {arguments}";
            Process.Start(new ProcessStartInfo
            {
                FileName = explorer,
                Arguments = target,
                UseShellExecute = false
            });
            detail = "started via explorer";
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static bool TryStartWithShellToken(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool hideWindow,
        out string detail)
    {
        var shell = GetShellWindow();
        if (shell == IntPtr.Zero)
        {
            detail = "no Explorer shell window";
            return false;
        }

        _ = GetWindowThreadProcessId(shell, out var pid);
        if (pid == 0)
        {
            detail = "could not resolve Explorer pid";
            return false;
        }

        var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process == IntPtr.Zero)
        {
            detail = Win32("OpenProcess(Explorer)");
            return false;
        }

        try
        {
            if (!OpenProcessToken(process, TokenDuplicate, out var token))
            {
                detail = Win32("OpenProcessToken(Explorer)");
                return false;
            }

            try
            {
                if (!DuplicateTokenEx(
                        token,
                        TokenAssignPrimary | TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAdjustSessionId,
                        IntPtr.Zero,
                        SecurityImpersonation,
                        TokenPrimary,
                        out var primary))
                {
                    detail = Win32("DuplicateTokenEx");
                    return false;
                }

                try
                {
                    var command = new StringBuilder();
                    command.Append(Quote(fileName));
                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        command.Append(' ').Append(arguments);
                    }

                    var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
                    var creationFlags = CreateUnicodeEnvironment;
                    if (hideWindow)
                    {
                        startup.dwFlags = StartfUseShowWindow;
                        startup.wShowWindow = SwHide;
                        creationFlags |= CreateNoWindow;
                    }

                    if (!CreateProcessWithTokenW(
                            primary,
                            LogonWithProfile,
                            fileName.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase) ? null : fileName,
                            command,
                            creationFlags,
                            IntPtr.Zero,
                            string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                            ref startup,
                            out var info))
                    {
                        detail = Win32("CreateProcessWithTokenW");
                        return false;
                    }

                    CloseHandle(info.hThread);
                    CloseHandle(info.hProcess);
                    detail = "started via Explorer token (unelevated)";
                    return true;
                }
                finally
                {
                    CloseHandle(primary);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static string Quote(string value) =>
        value.StartsWith('"') ? value : $"\"{value}\"";

    private static string Win32(string op) =>
        $"{op}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}";

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenAdjustDefault = 0x0080;
    private const uint TokenAdjustSessionId = 0x0100;
    private const int TokenElevationType = 20;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint LogonWithProfile = 1;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;
    private const int StartfUseShowWindow = 0x00000001;
    private const short SwHide = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inherit, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        ref TokenElevation tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr token,
        uint logonFlags,
        string? applicationName,
        StringBuilder commandLine,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);
}
