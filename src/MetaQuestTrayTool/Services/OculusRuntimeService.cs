using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

public sealed class OculusRuntimeService
{
    public const string ServiceName = "OVRService";

    public bool IsInstalled { get; private set; }
    public string? InstallPath { get; private set; }
    public string ServiceStatus { get; private set; } = "Unknown";
    public bool ServiceExists { get; private set; }
    public bool IsServiceRunning => ServiceStatus.Equals("Running", StringComparison.OrdinalIgnoreCase);

    public string? DebugToolCliPath =>
        string.IsNullOrWhiteSpace(InstallPath)
            ? null
            : System.IO.Path.Combine(InstallPath, "Support", "oculus-diagnostics", "OculusDebugToolCLI.exe");

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
            return $"Access denied while changing {ServiceName}. Start the app as Administrator to control the service.";
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
}
