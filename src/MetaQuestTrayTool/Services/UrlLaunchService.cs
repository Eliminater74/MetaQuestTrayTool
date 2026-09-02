namespace MetaQuestTrayTool.Services;

public static class UrlLaunchService
{
    public static void Open(string url)
    {
        if (!IsAllowedWebUrl(url))
        {
            throw new ArgumentException("URL must start with http:// or https://.", nameof(url));
        }

        if (!SessionHelperClient.TryLaunchUri(url, out var detail))
        {
            throw new InvalidOperationException("Could not open URL: " + detail);
        }
    }

    internal static bool IsAllowedWebUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
