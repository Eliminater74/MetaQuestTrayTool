using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace MetaQuestTrayTool.Services;

public interface IPyrowaveTransport
{
    string Post(string url, string body);
}

/// <summary>
/// Builds the dashboard SetValues body used by tools/quest3/control.py at commit 1b529ef.
/// The tray does not edit session files and does not send RestartSteamvr.
/// </summary>
public static class PyrowaveControl
{
    public const string DashboardUrl = "http://127.0.0.1:8082/api/dashboard-request";
    public const string ResearchCommit = "1b529ef54b4f7b34a44c28ad9a54c6c4ac6887a7";

    public static string? TryBuild(string setting, string value, bool authorizedDisruption, out string detail)
    {
        if (!authorizedDisruption)
        {
            detail = "control.py at " + ResearchCommit + " reconnects the server or asks for a SteamVR restart. Nothing was sent.";
            return null;
        }

        var assignments = setting.Trim().ToLowerInvariant() switch
        {
            "bitrate" => Bitrate(value, out detail),
            "wavelet" => Wavelet(value, out detail),
            "chroma" => Chroma(value, out detail),
            "render" => Resolution("emulated_headset_view_resolution", value, out detail),
            "stream" => Resolution("transcoding_view_resolution", value, out detail),
            "wired" => Wired(value, out detail),
            "refresh" => Refuse("control.py apply() accepts a refresh only when it is listed in the runtime capabilities file. This tray does not invent that file.", out detail),
            "automatic-bitrate" => Refuse("Automatic bitrate is not written by tools/quest3/control.py at " + ResearchCommit + ".", out detail),
            _ => Refuse("That setting is not a verified control.py assignment.", out detail)
        };
        if (assignments is null)
        {
            return null;
        }

        var body = new JsonObject
        {
            ["SetValues"] = new JsonArray(assignments.Select(item => (JsonNode)item).ToArray())
        }.ToJsonString();
        if (body.Contains("RestartSteamvr", StringComparison.Ordinal))
        {
            detail = "RestartSteamvr is not sent.";
            return null;
        }

        detail = "Built a SetValues body for " + setting + ". RestartSteamvr is not included. " + detail;
        return body;
    }

    public static string Send(IPyrowaveTransport transport, string body)
    {
        if (body.Contains("RestartSteamvr", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("RestartSteamvr is not sent.");
        }

        return transport.Post(DashboardUrl, body);
    }

    public static string PostLocalhost(string body, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = timeout };
        using var request = new HttpRequestMessage(HttpMethod.Post, DashboardUrl);
        request.Headers.TryAddWithoutValidation("X-ALVR", "true");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = client.Send(request);
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static JsonObject[]? Bitrate(string value, out string detail)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var mbps) || mbps < 1 || mbps > 2000)
        {
            detail = "Bitrate must be an integer from 1 to 2000 Mbps, the range in control.py apply() at " + ResearchCommit + ".";
            return null;
        }

        detail = string.Empty;
        return
        [
            Assignment(["session_settings", "video", "bitrate", "mode", "variant"], "ConstantMbps"),
            Assignment(["session_settings", "video", "bitrate", "mode", "ConstantMbps"], mbps)
        ];
    }

    private static JsonObject[]? Chroma(string value, out string detail)
    {
        var mode = value.Trim();
        if (mode is not ("420" or "444"))
        {
            detail = "Chroma accepts 420 or 444, matching chroma_444 in control.py.";
            return null;
        }

        detail = string.Empty;
        return [Assignment(["session_settings", "video", "pyrowave", "chroma_444"], mode == "444")];
    }

    private static JsonObject[]? Resolution(string field, string value, out string detail)
    {
        var parts = value.Split(',');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            || width < 32 || width > 8192 || height < 32 || height > 8192)
        {
            detail = "Resolution must be width,height with both integers from 32 to 8192, as in resolution.py at " + ResearchCommit + ".";
            return null;
        }

        detail = "This assignment changes the variant to Absolute. control.py tells the user to restart SteamVR. This tray does not restart it.";
        var prefix = new[] { "session_settings", "video", field };
        return
        [
            Assignment([..prefix, "variant"], "Absolute"),
            Assignment([..prefix, "Absolute", "width"], width),
            Assignment([..prefix, "Absolute", "height", "set"], true),
            Assignment([..prefix, "Absolute", "height", "content"], height)
        ];
    }

    private static JsonObject[]? Wavelet(string value, out string detail)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        var wavelet = EnumValue(parts[0], "Cdf97", "Cdf53", "Haar");
        if (wavelet is null)
        {
            detail = "Wavelet accepts Cdf97, Cdf53, or Haar, from control.py apply().";
            return null;
        }

        if (wavelet is "Cdf53" or "Haar")
        {
            var path = parts.Length == 2 ? EnumValue(parts[1], "Auto", "Compute") : null;
            if (path is null)
            {
                detail = "CDF 5/3 and Haar require Compute or Auto decode. Fragment is refused, matching control.py apply().";
                return null;
            }

            detail = string.Empty;
            return
            [
                Assignment(["session_settings", "video", "pyrowave", "wavelet", "variant"], wavelet),
                Assignment(["session_settings", "video", "pyrowave", "decode_path", "variant"], path)
            ];
        }

        detail = string.Empty;
        return [Assignment(["session_settings", "video", "pyrowave", "wavelet", "variant"], wavelet)];
    }

    private static JsonObject[]? Wired(string value, out string detail)
    {
        _ = value;
        detail = "USB enable in control.py sets connection.wired_client_type to Custom io.github.jms1717.quest3pyrowave, pyrowave transport Tcp, and stream_protocol Tcp, then posts UpdateClientList and tells the user to restart SteamVR. This tray does not send UpdateClientList or restart SteamVR, so wired mode stays unavailable.";
        return null;
    }

    private static string? EnumValue(string value, params string[] accepted) =>
        accepted.FirstOrDefault(item => string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static JsonObject[]? Refuse(string reason, out string detail)
    {
        detail = reason;
        return null;
    }

    private static JsonObject Assignment(string[] path, JsonNode value) => new()
    {
        ["path"] = new JsonArray(path.Select(segment => (JsonNode)new JsonObject { ["Name"] = segment }).ToArray()),
        ["value"] = value
    };
}
