using System.Diagnostics;

namespace MetaQuestTrayTool.Services;

public static class UrlLaunchService
{
    public static void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URL must start with http:// or https://.", nameof(url));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
