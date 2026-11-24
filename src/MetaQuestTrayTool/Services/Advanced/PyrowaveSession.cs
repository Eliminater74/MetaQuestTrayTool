namespace MetaQuestTrayTool.Services;

public sealed class PyrowaveSettingsReport
{
    public string ActiveProfile { get; init; } = "unavailable";
    public string Refresh { get; init; } = "unavailable";
    public string StreamResolution { get; init; } = "unavailable";
    public string RenderResolution { get; init; } = "unavailable";
    public string Bitrate { get; init; } = "unavailable";
    public string AutomaticBitrate { get; init; } = "unavailable";
    public string Wavelet { get; init; } = "unavailable";
    public string Chroma { get; init; } = "unavailable";
    public string WiredConnections { get; init; } = "unavailable";
    public string Telemetry { get; init; } = "unavailable";
    public string WriteStatus { get; init; } = "writes refused until a verified schema path exists";

    public string ToDisplayText() => string.Join(Environment.NewLine, [
        "Active profile: " + ActiveProfile,
        "Refresh: " + Refresh,
        "Stream resolution: " + StreamResolution,
        "Render resolution: " + RenderResolution,
        "Bitrate: " + Bitrate,
        "Automatic bitrate: " + AutomaticBitrate,
        "Wavelet: " + Wavelet,
        "Chroma: " + Chroma,
        "Wired connections: " + WiredConnections,
        "Telemetry: " + Telemetry,
        WriteStatus
    ]);
}

public static class PyrowaveSession
{
    public static PyrowaveSettingsReport Read(string? sessionText)
    {
        var profiles = PyrowaveDiscovery.DiscoverProfiles(sessionText);
        return new PyrowaveSettingsReport
        {
            ActiveProfile = profiles.Count == 0 ? "unavailable" : string.Join(", ", profiles),
            WriteStatus = "Configuration writes are unavailable. Bitrate, resolution, wavelet, chroma, and wired-connection fields were not changed."
        };
    }

    public static string? TryApply(string? json, string setting, string value, bool serverRunning, out string detail)
    {
        _ = setting;
        _ = value;
        _ = serverRunning;
        if (string.IsNullOrWhiteSpace(json) || !AlvrSessionEditor.LooksLikePyrowaveSession(json))
        {
            detail = "This is not a verified Pyrowave session. Stock ALVR was not edited.";
            return null;
        }

        detail = "Pyrowave configuration writes are unavailable. The session schema for this setting was not verified, so nothing was written.";
        return null;
    }
}
