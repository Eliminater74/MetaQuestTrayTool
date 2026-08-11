using IOPath = System.IO.Path;

namespace MetaQuestTrayTool.Services;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = IOPath.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MetaQuestTrayTool");

    public static string SettingsFile { get; } = IOPath.Combine(AppDataDirectory, "settings.json");
    public static string LogFile { get; } = IOPath.Combine(AppDataDirectory, "app.log");

    public static void EnsureAppDataDirectory()
    {
        System.IO.Directory.CreateDirectory(AppDataDirectory);
    }
}
