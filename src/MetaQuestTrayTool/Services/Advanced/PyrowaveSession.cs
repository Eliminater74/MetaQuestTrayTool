using System.Text.Json.Nodes;

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
        var profile = profiles.Count == 0 ? "unavailable" : string.Join(", ", profiles);
        var refresh = "unavailable";
        var stream = "unavailable";
        var render = "unavailable";
        var bitrate = "unavailable";
        var automatic = "unavailable";
        var wavelet = "unavailable";
        var chroma = "unavailable";
        var wired = "unavailable";
        var telemetry = "unavailable";
        if (!string.IsNullOrWhiteSpace(sessionText))
        {
            try
            {
                if (JsonNode.Parse(sessionText) is JsonObject root
                    && root["session_settings"]?["video"] is JsonObject video)
                {
                    refresh = video["preferred_fps"]?.ToString() ?? refresh;
                    stream = DescribeFrame(video["transcoding_view_resolution"] as JsonObject);
                    render = DescribeFrame(video["emulated_headset_view_resolution"] as JsonObject);
                    if (video["bitrate"]?["mode"] is JsonObject mode)
                    {
                        var variant = mode["variant"]?.ToString() ?? "unknown";
                        automatic = variant == "Adaptive" ? "Adaptive is present. control.py at 1b529ef does not write it." : "not adaptive";
                        bitrate = variant + " " + (mode["ConstantMbps"]?.ToString() ?? "unavailable");
                    }

                    if (video["pyrowave"] is JsonObject pyro)
                    {
                        wavelet = pyro["wavelet"]?["variant"]?.ToString() ?? wavelet;
                        chroma = pyro["chroma_444"]?.ToString() switch
                        {
                            "true" => "444",
                            "false" => "420",
                            _ => chroma
                        };
                        wired = pyro["transport"]?["variant"]?.ToString() ?? wired;
                        telemetry = pyro["latency_stamp"] is null
                            ? "No latency_stamp field in this session. Log telemetry is not inferred."
                            : "latency_stamp=" + pyro["latency_stamp"];
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        return new PyrowaveSettingsReport
        {
            ActiveProfile = profile,
            Refresh = refresh,
            StreamResolution = stream,
            RenderResolution = render,
            Bitrate = bitrate,
            AutomaticBitrate = automatic,
            Wavelet = wavelet,
            Chroma = chroma,
            WiredConnections = wired,
            Telemetry = telemetry,
            WriteStatus = "Session files are not edited. Dashboard SetValues from tools/quest3/control.py at 1b529ef can be built separately and is not sent unless a caller authorizes the disruption. RestartSteamvr is never sent."
        };
    }

    private static string DescribeFrame(JsonObject? frame)
    {
        if (frame is null)
        {
            return "unavailable";
        }

        var variant = frame["variant"]?.ToString() ?? "unknown";
        if (variant == "Absolute")
        {
            return "Absolute " + frame["Absolute"]?["width"] + "x" + frame["Absolute"]?["height"]?["content"];
        }

        return variant + " " + frame["Scale"];
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
