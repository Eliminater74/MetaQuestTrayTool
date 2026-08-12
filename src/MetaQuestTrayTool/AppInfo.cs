using System.Reflection;

namespace MetaQuestTrayTool;

/// <summary>
/// Display name, version, and donate URL.
/// Version and product name come from Directory.Build.props (change them there).
/// Donate URL is the live PayPal page.
/// </summary>
public static class AppInfo
{
    public const string Author = "Eliminater74";

    public static string ProductName =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Meta Quest Tray Tool";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Live PayPal donate page.</summary>
    public const string DonateUrl =
        "https://www.paypal.com/donate/?business=X76ZW4RHA6T9C&no_recurring=0&item_name=Eliminater74+builds+Meta+Quest+Tray+Tool+%E2%80%94+free+Quest+Link+%26+SteamVR+tray+settings.+Your+gift+keeps+it+going.&currency_code=USD";

    /// <summary>PayPal “Why are you asking for money?” box (127 char max). Display name: Eliminater74.</summary>
    public const string DonateBlurb =
        "Eliminater74 builds Meta Quest Tray Tool — free Quest Link & SteamVR tray settings. Your gift keeps it going.";

    public static bool HasDonateUrl =>
        DonateUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
