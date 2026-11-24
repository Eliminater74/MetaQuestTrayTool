using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace MetaQuestTrayTool.Services;

public sealed class AlvrClientObservation
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ConnectionState { get; init; } = "unknown";
    public bool Cabled { get; init; }
}

public sealed class AlvrSessionReport
{
    public string PreferredFps { get; init; } = "unavailable";
    public string Bitrate { get; init; } = "unavailable";
    public string StreamResolution { get; init; } = "unavailable";
    public string RenderResolution { get; init; } = "unavailable";
    public string Telemetry { get; init; } = "Live telemetry is not stored in session.json.";
    public string Profiles { get; init; } = "Named ALVR profiles are not a field in the verified session.json sample. None are applied.";
    public IReadOnlyList<AlvrClientObservation> Clients { get; init; } = [];
    public string RestartNote { get; init; } = "preferred_fps and both resolution fields are marked steamvr-restart in ALVR settings.rs at dc3abf3b. This tray does not restart SteamVR.";

    public string ToDisplayText() => string.Join(Environment.NewLine, new[]
    {
        "Preferred FPS: " + PreferredFps,
        "Bitrate: " + Bitrate,
        "Stream resolution: " + StreamResolution,
        "Render resolution: " + RenderResolution,
        "Clients: " + (Clients.Count == 0 ? "none" : string.Join("; ", Clients.Select(client => client.DisplayName + " " + client.ConnectionState + (client.Cabled ? " cabled" : "")))),
        Telemetry,
        Profiles,
        RestartNote
    });
}

/// <summary>
/// Reads and edits an ALVR session.json using shapes from the public session sample in alvr-org/ALVR issue 2107
/// and the numeric ranges in alvr/session/src/settings.rs at dc3abf3b.
/// Unknown fields are left in place. The server-running refusal has no bypass.
/// </summary>
public static class AlvrSessionManagement
{
    public const double BitrateMinimum = 5;
    public const double BitrateMaximum = 1000;
    public const int ResolutionMinimum = 32;
    public const int ResolutionMaximum = 8192;

    public static AlvrSessionReport Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonObject root)
        {
            return new AlvrSessionReport();
        }

        var video = root["session_settings"]?["video"] as JsonObject;
        return new AlvrSessionReport
        {
            PreferredFps = DescribeNumber(video?["preferred_fps"]),
            Bitrate = DescribeBitrate(video?["bitrate"] as JsonObject),
            StreamResolution = DescribeFrame(video?["transcoding_view_resolution"] as JsonObject),
            RenderResolution = DescribeFrame(video?["emulated_headset_view_resolution"] as JsonObject),
            Clients = ReadClients(root["client_connections"] as JsonObject)
        };
    }

    public static string? TrySetConstantBitrate(string json, double mbps, bool serverRunning, out string detail)
    {
        if (serverRunning)
        {
            detail = "The server appears to be running. The session file was not edited. There is no override that bypasses this.";
            return null;
        }

        if (mbps < BitrateMinimum || mbps > BitrateMaximum || double.IsNaN(mbps) || double.IsInfinity(mbps))
        {
            detail = "Bitrate is outside 5 to 1000 Mbps from ALVR settings.rs at dc3abf3b. The file was not edited.";
            return null;
        }

        if (!TryObject(json, out var root, out detail) || root is null)
        {
            return null;
        }

        if (root["session_settings"]?["video"]?["bitrate"]?["mode"] is not JsonObject mode)
        {
            detail = "bitrate.mode is missing. It was not created.";
            return null;
        }

        if (mode["variant"]?.GetValue<string>() != "ConstantMbps" || mode["ConstantMbps"] is not JsonValue)
        {
            detail = "The bitrate mode is not an existing ConstantMbps number. The variant was not changed.";
            return null;
        }

        mode["ConstantMbps"] = mbps;
        detail = "Updated the existing ConstantMbps value. Other fields were preserved. SteamVR was not restarted.";
        return root.ToJsonString();
    }

    public static string? TrySetResolution(string json, string field, string kind, double value, bool serverRunning, out string detail)
    {
        if (field is not ("transcoding_view_resolution" or "emulated_headset_view_resolution"))
        {
            detail = "That resolution field is not one of the two verified video fields.";
            return null;
        }

        if (serverRunning)
        {
            detail = "The server appears to be running. The session file was not edited. There is no override that bypasses this.";
            return null;
        }

        if (!TryObject(json, out var root, out detail) || root is null)
        {
            return null;
        }

        if (root["session_settings"]?["video"]?[field] is not JsonObject frame)
        {
            detail = "The resolution object is missing. It was not created.";
            return null;
        }

        var variant = frame["variant"]?.GetValue<string>();
        if (kind == "scale")
        {
            if (variant != "Scale" || frame["Scale"] is not JsonValue)
            {
                detail = "Scale is not the active existing value. The variant was not switched.";
                return null;
            }

            if (value < 0.25 || value > 2)
            {
                detail = "Scale is outside 0.25 to 2 from ALVR settings.rs. The file was not edited.";
                return null;
            }

            frame["Scale"] = value;
            detail = "Updated the existing scale. SteamVR was not restarted. The schema marks this field steamvr-restart.";
            return root.ToJsonString();
        }

        if (variant != "Absolute" || frame["Absolute"] is not JsonObject absolute)
        {
            detail = "Absolute is not the active existing value. The variant was not switched.";
            return null;
        }

        if (kind is not ("width" or "height"))
        {
            detail = "Resolution edits accept width, height, or scale.";
            return null;
        }

        if (value < ResolutionMinimum || value > ResolutionMaximum || Math.Abs(value % 32) > 0.001)
        {
            detail = "Resolution must be a multiple of 32 from 32 to 8192. The file was not edited.";
            return null;
        }

        if (kind == "width")
        {
            if (absolute["width"] is not JsonValue)
            {
                detail = "Absolute width is missing. It was not created.";
                return null;
            }

            absolute["width"] = (int)value;
        }
        else
        {
            if (absolute["height"] is not JsonObject height || height["set"]?.GetValue<bool>() != true || height["content"] is not JsonValue)
            {
                detail = "Absolute height is not an existing set value. It was not created.";
                return null;
            }

            height["content"] = (int)value;
        }

        detail = "Updated the existing absolute resolution. SteamVR was not restarted. The schema marks this field steamvr-restart.";
        return root.ToJsonString();
    }

    public static string? RestoreBackup(string path, bool serverRunning, out string detail)
    {
        if (serverRunning)
        {
            detail = "The server appears to be running. The backup was not restored.";
            return null;
        }

        var backup = path + ".bak";
        if (!File.Exists(backup) || !AlvrSessionEditor.IsSafeDirectory(path))
        {
            detail = "No backup was found, or the path was rejected. Nothing was written.";
            return null;
        }

        var contents = File.ReadAllText(backup);
        AlvrSessionEditor.WriteWithBackup(path, contents);
        detail = "Restored session.json from the adjacent .bak. The previous file is now the new backup. SteamVR was not restarted.";
        return contents;
    }

    private static bool TryObject(string json, out JsonObject? root, out string detail)
    {
        root = null;
        try
        {
            if (JsonNode.Parse(json) is JsonObject parsed)
            {
                root = parsed;
                detail = string.Empty;
                return true;
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        detail = "The session file is not a JSON object.";
        return false;
    }

    private static string DescribeNumber(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number)
            ? number.ToString("0.##", CultureInfo.InvariantCulture)
            : "unavailable";

    private static string DescribeBitrate(JsonObject? bitrate)
    {
        var mode = bitrate?["mode"] as JsonObject;
        if (mode is null)
        {
            return "unavailable";
        }

        var variant = mode["variant"]?.GetValue<string>() ?? "unknown";
        var constant = mode["ConstantMbps"] is JsonValue value && value.TryGetValue<double>(out var number)
            ? number.ToString("0.##", CultureInfo.InvariantCulture)
            : "not a plain number";
        return variant + " " + constant + " Mbps";
    }

    private static string DescribeFrame(JsonObject? frame)
    {
        if (frame is null)
        {
            return "unavailable";
        }

        var variant = frame["variant"]?.GetValue<string>() ?? "unknown";
        if (variant == "Scale" && frame["Scale"] is JsonValue scale && scale.TryGetValue<double>(out var factor))
        {
            return "Scale " + factor.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (variant == "Absolute" && frame["Absolute"] is JsonObject absolute && absolute["width"] is JsonValue width && width.TryGetValue<int>(out var pixels))
        {
            var height = absolute["height"]?["content"]?.ToString() ?? "unset";
            return "Absolute " + pixels.ToString(CultureInfo.InvariantCulture) + "x" + height;
        }

        return variant;
    }

    private static IReadOnlyList<AlvrClientObservation> ReadClients(JsonObject? clients)
    {
        if (clients is null)
        {
            return [];
        }

        var list = new List<AlvrClientObservation>();
        foreach (var entry in clients)
        {
            if (entry.Value is not JsonObject client)
            {
                continue;
            }

            list.Add(new AlvrClientObservation
            {
                Key = entry.Key,
                DisplayName = client["display_name"]?.GetValue<string>() ?? entry.Key,
                ConnectionState = client["connection_state"]?.GetValue<string>() ?? "unknown",
                Cabled = client["cabled"]?.GetValue<bool>() == true
            });
        }

        return list;
    }
}
