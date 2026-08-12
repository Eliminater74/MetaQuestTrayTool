using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

public sealed class StartupApplyResult
{
    public bool StartWithWindows { get; init; }
    public bool AsAdministrator { get; init; }
    public bool RecommendRestartElevated { get; init; }
}

/// <summary>
/// HKCU Run for normal logon start, or a logon scheduled task with highest
/// privileges so the tray process itself is elevated (OTT-style). A Windows
/// Service cannot own a tray icon because of Session 0 isolation.
/// </summary>
public sealed class StartupRegistrationService
{
    public const string TaskName = "MetaQuestTrayTool";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MetaQuestTrayTool";

    public bool IsProcessElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool IsRunKeyEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public bool IsAdministratorTaskRegistered()
    {
        return RunSchtasks($"/Query /TN \"{TaskName}\"", elevate: false) == 0;
    }

    public string DescribeStatus()
    {
        var rights = IsProcessElevated
            ? "Running with Administrator rights."
            : "Running without Administrator rights.";

        if (IsAdministratorTaskRegistered())
        {
            return rights + " Logon start is an elevated scheduled task (no UAC after the one-time install).";
        }

        if (IsRunKeyEnabled())
        {
            return rights + " Logon start is the standard user Startup entry.";
        }

        return rights + " Not set to start with Windows.";
    }

    public void SyncFromSystem(Models.AppSettings settings)
    {
        var task = IsAdministratorTaskRegistered();
        var run = IsRunKeyEnabled();
        settings.StartWithWindows = run || task;
        settings.StartWithWindowsAsAdministrator = task;
    }

    /// <summary>
    /// Installs or removes the chosen startup mode. Enabling administrator
    /// start implies Start with Windows. Administrator mode replaces the
    /// HKCU Run entry so the app does not launch twice.
    /// </summary>
    public StartupApplyResult Apply(bool startWithWindows, bool asAdministrator)
    {
        if (asAdministrator)
        {
            startWithWindows = true;
        }

        if (!startWithWindows)
        {
            asAdministrator = false;
        }

        var wasElevatedTask = IsAdministratorTaskRegistered();

        if (asAdministrator)
        {
            CreateAdministratorTask();
            SetRunKey(false);
        }
        else
        {
            DeleteAdministratorTask();
            SetRunKey(startWithWindows);
        }

        return new StartupApplyResult
        {
            StartWithWindows = startWithWindows,
            AsAdministrator = asAdministrator,
            RecommendRestartElevated = asAdministrator && !wasElevatedTask && !IsProcessElevated
        };
    }

    public void RestartElevated()
    {
        var exePath = RequireExePath();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--restart",
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Administrator approval was cancelled.", ex);
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void SetRunKey(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{RequireExePath()}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private void CreateAdministratorTask()
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool.startup.xml");
        File.WriteAllText(xmlPath, BuildTaskXml(), Encoding.Unicode);
        try
        {
            var code = RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F", elevate: !IsProcessElevated);
            if (code != 0)
            {
                throw new InvalidOperationException(
                    "Could not create the elevated logon task. Approve the UAC prompt if it appears.");
            }
        }
        finally
        {
            try
            {
                File.Delete(xmlPath);
            }
            catch
            {
                // temp file is best-effort
            }
        }
    }

    private void DeleteAdministratorTask()
    {
        if (!IsAdministratorTaskRegistered())
        {
            return;
        }

        var code = RunSchtasks($"/Delete /TN \"{TaskName}\" /F", elevate: !IsProcessElevated);
        if (code != 0 && IsAdministratorTaskRegistered())
        {
            throw new InvalidOperationException(
                "Could not remove the elevated logon task. Approve the UAC prompt if it appears.");
        }
    }

    private string BuildTaskXml()
    {
        var userId = WindowsIdentity.GetCurrent().Name;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = $@"{Environment.UserDomainName}\{Environment.UserName}";
        }

        var exe = SecurityElement.Escape(RequireExePath()) ?? RequireExePath();
        var user = SecurityElement.Escape(userId) ?? userId;

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>Meta Quest Tray Tool</Author>
                <Description>Starts Meta Quest Tray Tool at logon with Administrator rights so OpenXR, OVRService, and similar tasks do not prompt every time.</Description>
                <URI>\{TaskName}</URI>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>false</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exe}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static string RequireExePath()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("Could not determine the application path.");
        }

        return exePath;
    }

    private static int RunSchtasks(string arguments, bool elevate)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            Arguments = arguments,
            UseShellExecute = elevate,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (elevate)
        {
            start.Verb = "runas";
        }
        else
        {
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return -1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Administrator approval was cancelled.", ex);
        }
    }
}
