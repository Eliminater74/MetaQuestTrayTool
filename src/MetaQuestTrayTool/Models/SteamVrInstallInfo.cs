namespace MetaQuestTrayTool.Models;

public enum SteamVrChannel
{
    Unknown,
    NotInstalled,
    Stable,
    Beta
}

/// <summary>Detected SteamVR install from Steam library + appmanifest_250820.acf.</summary>
public sealed record SteamVrInstallInfo
{
    public bool IsInstalled { get; init; }
    public SteamVrChannel Channel { get; init; } = SteamVrChannel.NotInstalled;
    public string? Version { get; init; }
    public string? BuildId { get; init; }
    public string? BetaKey { get; init; }
    public string? InstallPath { get; init; }
    public string? ManifestPath { get; init; }
    public string? SteamRoot { get; init; }
    public bool IsRunning { get; init; }

    public string ChannelLabel => Channel switch
    {
        SteamVrChannel.Stable => "Stable",
        SteamVrChannel.Beta => string.IsNullOrWhiteSpace(BetaKey) ? "Beta" : $"Beta ({BetaKey})",
        SteamVrChannel.NotInstalled => "Not installed",
        _ => "Unknown"
    };

    /// <summary>One-line banner for Info / tray.</summary>
    public string Banner
    {
        get
        {
            if (!IsInstalled)
            {
                return "SteamVR: not installed — open Steam to install.";
            }

            var version = string.IsNullOrWhiteSpace(Version) ? "version unknown" : $"v{Version}";
            var build = string.IsNullOrWhiteSpace(BuildId) ? string.Empty : $" · build {BuildId}";
            var running = IsRunning ? " · running" : string.Empty;
            return $"SteamVR: {version} · {ChannelLabel}{build}{running}";
        }
    }

    public string Detail
    {
        get
        {
            if (!IsInstalled)
            {
                return "SteamVR (app 250820) was not found in any Steam library.";
            }

            var parts = new List<string> { ChannelLabel };
            if (!string.IsNullOrWhiteSpace(Version))
            {
                parts.Add($"file version {Version}");
            }

            if (!string.IsNullOrWhiteSpace(BuildId))
            {
                parts.Add($"Steam build {BuildId}");
            }

            if (!string.IsNullOrWhiteSpace(InstallPath))
            {
                parts.Add(InstallPath);
            }

            return string.Join(" · ", parts);
        }
    }

    public static SteamVrInstallInfo Missing(string? steamRoot = null) => new()
    {
        IsInstalled = false,
        Channel = SteamVrChannel.NotInstalled,
        SteamRoot = steamRoot
    };
}
