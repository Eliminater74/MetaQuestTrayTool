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

        if (!SessionHelperClient.TryLaunchUri(url, out var detail))
        {
            throw new InvalidOperationException("Could not open URL: " + detail);
        }
    }
}
