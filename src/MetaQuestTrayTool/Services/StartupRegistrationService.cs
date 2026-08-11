using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MetaQuestTrayTool";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException("Could not determine the application path.");
            }

            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
