using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

public sealed class OculusRuntimeService
{
    public const string ServiceName = "OVRService";
    private const int SwRestore = 9;

    public bool IsInstalled { get; private set; }
    public string? InstallPath { get; private set; }
    public string ServiceStatus { get; private set; } = "Unknown";
    public bool ServiceExists { get; private set; }
    public bool IsServiceRunning => ServiceStatus.Equals("Running", StringComparison.OrdinalIgnoreCase);

    public string? DebugToolCliPath =>
        string.IsNullOrWhiteSpace(InstallPath)
            ? null
            : Path.Combine(InstallPath, "Support", "oculus-diagnostics", "OculusDebugToolCLI.exe");

    public string? DebugToolGuiPath =>
        string.IsNullOrWhiteSpace(InstallPath)
            ? null
            : Path.Combine(InstallPath, "Support", "oculus-diagnostics", "OculusDebugTool.exe");

    public void Refresh()
    {
        InstallPath = DetectInstallPath();
        IsInstalled = !string.IsNullOrWhiteSpace(InstallPath) && Directory.Exists(InstallPath);

        try
        {
            using var controller = new ServiceController(ServiceName);
            controller.Refresh();
            ServiceExists = true;
            ServiceStatus = controller.Status.ToString();
        }
        catch
        {
            ServiceExists = false;
            ServiceStatus = "Not found";
        }
    }

    public string DescribeStatus()
    {
        var install = IsInstalled ? InstallPath : "not found";
        return $"Oculus install: {install}. {ServiceName}: {ServiceStatus}.";
    }

    public string Start() => ChangeState(start: true);
    public string Stop() => ChangeState(start: false);

    public string Restart()
    {
        Refresh();
        if (!ServiceExists)
        {
            return $"{ServiceName} was not found.";
        }

        if (IsServiceRunning)
        {
            var stopResult = Stop();
            if (!IsServiceRunning && stopResult.Contains("denied", StringComparison.OrdinalIgnoreCase))
            {
                return stopResult;
            }
        }

        return Start();
    }

    /// <summary>
    /// Opens Meta Horizon Link (oculus-client). If it is already in the tray, starting
    /// Client.exe again normally restores the full window.
    /// </summary>
    public string ShowMetaHorizonLink()
    {
        Refresh();
        var clientPath = ResolveClientExePath();
        if (clientPath is null)
        {
            return "Meta Horizon Link client was not found (expected Support\\oculus-client\\Client.exe).";
        }

        if (TryActivateExistingClient(clientPath))
        {
            return "Brought Meta Horizon Link to the foreground.";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = clientPath,
                WorkingDirectory = Path.GetDirectoryName(clientPath) ?? InstallPath ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });
            return $"Opened Meta Horizon Link ({Path.GetFileName(clientPath)}).";
        }
        catch (Exception ex)
        {
            return $"Could not open Meta Horizon Link: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the official Oculus Debug Tool GUI (same as classic OTT). Must run from
    /// Support\oculus-diagnostics so it matches the installed Meta runtime.
    /// </summary>
    public string ShowOculusDebugTool()
    {
        Refresh();
        var guiPath = DebugToolGuiPath;
        if (string.IsNullOrWhiteSpace(guiPath) || !File.Exists(guiPath))
        {
            return "Oculus Debug Tool was not found (expected Support\\oculus-diagnostics\\OculusDebugTool.exe).";
        }

        if (TryActivateExistingDebugTool(guiPath))
        {
            return "Brought Oculus Debug Tool to the foreground.";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = guiPath,
                WorkingDirectory = Path.GetDirectoryName(guiPath) ?? InstallPath ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });
            return $"Opened Oculus Debug Tool ({Path.GetFileName(guiPath)}).";
        }
        catch (Exception ex)
        {
            return $"Could not open Oculus Debug Tool: {ex.Message}";
        }
    }

    public string? ResolveClientExePath()
    {
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            return null;
        }

        string[] candidates =
        [
            Path.Combine(InstallPath, "Support", "oculus-client", "Client.exe"),
            Path.Combine(InstallPath, "Support", "oculus-client", "OculusClient.exe"),
            Path.Combine(InstallPath, "OculusClient.exe"),
            Path.Combine(InstallPath, "Client.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool TryActivateExistingClient(string clientPath)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                using (process)
                {
                    string? path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // Access denied for some processes — fall back to name check below.
                    }

                    var isExactPath = path is not null
                                      && string.Equals(path, clientPath, StringComparison.OrdinalIgnoreCase);
                    if (!isExactPath)
                    {
                        var name = process.ProcessName;
                        if (!name.Equals("Client", StringComparison.OrdinalIgnoreCase)
                            && !name.Equals("OculusClient", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // "Client" is common — only trust if path matches oculus-client.
                        if (path is null || !path.Contains("oculus-client", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        return false;
                    }

                    ShowWindow(hwnd, SwRestore);
                    SetForegroundWindow(hwnd);
                    return true;
                }
            }
            catch
            {
                // Ignore processes we cannot inspect.
            }
        }

        return false;
    }

    private static bool TryActivateExistingDebugTool(string guiPath)
    {
        foreach (var process in Process.GetProcessesByName("OculusDebugTool"))
        {
            try
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (path is not null
                            && !string.Equals(path, guiPath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        // Access denied — still try to restore by process name.
                    }

                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    ShowWindow(hwnd, SwRestore);
                    SetForegroundWindow(hwnd);
                    return true;
                }
            }
            catch
            {
                // Ignore processes we cannot inspect.
            }
        }

        return false;
    }

    private string ChangeState(bool start)
    {
        Refresh();
        if (!ServiceExists)
        {
            return $"{ServiceName} was not found. Is the Meta Quest / Oculus PC software installed?";
        }

        try
        {
            using var controller = new ServiceController(ServiceName);
            controller.Refresh();

            if (start)
            {
                if (controller.Status == ServiceControllerStatus.Running)
                {
                    return $"{ServiceName} is already running.";
                }

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            else
            {
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    return $"{ServiceName} is already stopped.";
                }

                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            Refresh();
            return $"{ServiceName} is now {ServiceStatus}.";
        }
        catch (InvalidOperationException ex) when (ex.InnerException is System.ComponentModel.Win32Exception win32
                                                   && win32.NativeErrorCode is 5)
        {
            return $"Access denied while changing {ServiceName}. The tray must already be running as Administrator "
                   + "(automatic at logon) — you cannot approve UAC with the headset on.";
        }
        catch (Exception ex)
        {
            return $"Could not change {ServiceName}: {ex.Message}";
        }
    }

    private static string? DetectInstallPath()
    {
        string[] registryPaths =
        [
            @"SOFTWARE\Oculus VR, LLC\Oculus",
            @"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus"
        ];

        foreach (var path in registryPaths)
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            var value = key?.GetValue("Base") as string ?? key?.GetValue("InitialInstallDir") as string;
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                return value.TrimEnd('\\', '/');
            }
        }

        var defaultPath = @"C:\Program Files\Oculus";
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
