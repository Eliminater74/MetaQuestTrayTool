using System.Reflection;

namespace MetaQuestTrayTool;

/// <summary>
/// Display name, version, and donate URL.
/// Version and product name come from Directory.Build.props (change them there).
/// Paste the live donate URL into <see cref="DonateUrl"/> when you have it.
/// </summary>
public static class AppInfo
{
    public const string Author = "Eliminater74";

    public static string ProductName =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Meta Quest Tray Tool";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>PayPal / donation page URL. Paste the live link here when you have it.</summary>
    public const string DonateUrl = "";

    /// <summary>PayPal “Why are you asking for money?” box (127 char max). Display name: Eliminater74.</summary>
    public const string DonateBlurb =
        "Eliminater74 builds Meta Quest Tray Tool — free Quest Link & SteamVR tray settings. Your gift keeps it going.";

    public static bool HasDonateUrl =>
        DonateUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
