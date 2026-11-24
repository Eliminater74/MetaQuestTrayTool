using System.IO;
using System.Text.Json.Nodes;

namespace MetaQuestTrayTool.Services;

public enum PcvrProviderKind
{
    Unknown,
    MetaQuestLink,
    SteamVr,
    SteamLink,
    VirtualDesktop,
    Alvr,
    Pyrowave
}

public sealed record ProviderObservation(
    PcvrProviderKind Kind,
    bool Installed,
    bool Running,
    bool ActiveStream,
    string Detail);

public sealed class ProviderSignals
{
    public bool MetaLinkStreaming { get; init; }
    public bool SteamVrRunning { get; init; }
    public bool SteamLinkProcess { get; init; }
    public bool VirtualDesktopProcess { get; init; }
    public bool AlvrProcess { get; init; }
    public bool PyrowaveMarker { get; init; }
    public bool AlvrInstalled { get; init; }
    public bool PyrowaveInstalled { get; init; }
}

public static class ProviderClassifier
{
    public static IReadOnlyList<ProviderObservation> Classify(ProviderSignals signals)
    {
        var results = new List<ProviderObservation>();
        if (signals.MetaLinkStreaming)
        {
            results.Add(new ProviderObservation(
                PcvrProviderKind.MetaQuestLink,
                true,
                true,
                true,
                "Meta Quest Link or Air Link is the active stream."));
        }

        if (signals.SteamVrRunning)
        {
            var overLink = signals.MetaLinkStreaming;
            results.Add(new ProviderObservation(
                PcvrProviderKind.SteamVr,
                true,
                true,
                !overLink,
                overLink
                    ? "SteamVR is running over Meta Link. This is not the Steam Link application."
                    : "SteamVR is running without a detected Meta Link stream."));
        }

        if (signals.SteamLinkProcess && !signals.MetaLinkStreaming)
        {
            results.Add(new ProviderObservation(
                PcvrProviderKind.SteamLink,
                true,
                true,
                !signals.AlvrProcess && !signals.PyrowaveMarker,
                "The Steam Link process is running and Meta Link is not the active stream."));
        }

        if (signals.VirtualDesktopProcess)
        {
            results.Add(new ProviderObservation(
                PcvrProviderKind.VirtualDesktop,
                true,
                true,
                !signals.MetaLinkStreaming,
                "Virtual Desktop streamer process is running."));
        }

        if (signals.AlvrInstalled || signals.AlvrProcess)
        {
            results.Add(new ProviderObservation(
                PcvrProviderKind.Alvr,
                signals.AlvrInstalled || signals.AlvrProcess,
                signals.AlvrProcess && !signals.PyrowaveMarker,
                signals.AlvrProcess && !signals.PyrowaveMarker && !signals.MetaLinkStreaming,
                signals.PyrowaveMarker
                    ? "An ALVR-shaped install is present, but a Pyrowave marker takes precedence."
                    : "Stock ALVR was detected. Session edits are refused while its server is running unless explicitly overridden."));
        }

        if (signals.PyrowaveInstalled || signals.PyrowaveMarker)
        {
            results.Add(new ProviderObservation(
                PcvrProviderKind.Pyrowave,
                true,
                signals.PyrowaveMarker,
                signals.PyrowaveMarker && !signals.MetaLinkStreaming,
                "Quest3-Pyrowave is a separate provider. Package io.github.jms1717.quest3pyrowave is reported only. This tray does not install the APK."));
        }

        if (results.Count == 0)
        {
            results.Add(new ProviderObservation(PcvrProviderKind.Unknown, false, false, false, "No streaming provider was detected from the supplied signals."));
        }

        return results;
    }
}

public static class AlvrSessionEditor
{
    public static bool IsSafeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal) || path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0)
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path);
            return Path.IsPathRooted(full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static string? TrySetExistingNumber(string json, string dottedPath, double value, bool serverRunning, bool allowWhileRunning, out string detail)
    {
        if (serverRunning && !allowWhileRunning)
        {
            detail = "The server appears to be running. The session file was not edited.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(dottedPath) || dottedPath.Contains("..", StringComparison.Ordinal))
        {
            detail = "The setting path was rejected.";
            return null;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            detail = "The session file is not valid JSON.";
            return null;
        }

        if (root is not JsonObject current)
        {
            detail = "The session root is not an object.";
            return null;
        }

        var parts = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is not JsonObject child)
            {
                detail = "The setting path does not exist. Unknown fields were left untouched.";
                return null;
            }

            current = child;
        }

        var leaf = parts[^1];
        if (current[leaf] is not JsonValue existing || !existing.TryGetValue<double>(out _) && !existing.TryGetValue<int>(out _))
        {
            detail = "The existing value is missing or is not a number. It was not created or replaced.";
            return null;
        }

        current[leaf] = value;
        detail = "Updated an existing numeric value. Other fields were preserved.";
        return root.ToJsonString();
    }
}

public static class SteamVrDriverList
{
    public static IReadOnlyList<string> Read(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject;
        var array = root?["external_drivers"] as JsonArray;
        if (array is null)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0)
            {
                names.Add(text);
            }
        }

        return names;
    }

    public static string Add(string json, string driverPath, bool authorized)
    {
        if (!authorized)
        {
            throw new InvalidOperationException("Driver registration was not authorized. No driver was removed or added.");
        }

        if (!AlvrSessionEditor.IsSafeDirectory(driverPath))
        {
            throw new InvalidOperationException("The driver path was rejected.");
        }

        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("SteamVR driver list is not a JSON object.");
        var array = root["external_drivers"] as JsonArray ?? [];
        var existing = new List<string>();
        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<string>(out var text))
            {
                existing.Add(text);
            }
        }
        if (existing.Any(item => string.Equals(item, driverPath, StringComparison.OrdinalIgnoreCase)))
        {
            return root.ToJsonString();
        }

        array.Add(driverPath);
        root["external_drivers"] = array;
        return root.ToJsonString();
    }
}

public static class PyrowaveDiscovery
{
    public const string PackageName = "io.github.jms1717.quest3pyrowave";
    public static readonly string[] ResearchProfileNames =
    [
        "Starter 72 Hz",
        "Quality 120 Hz",
        "Godlike 90 Hz",
        "Godlike 120 Hz",
        "Colour 4:4:4",
        "Wi-Fi Quality 120 Hz",
        "Wi-Fi 90 Hz"
    ];

    public static bool LooksLikeInstall(string? directory, string? versionText)
    {
        if (!string.IsNullOrWhiteSpace(versionText)
            && versionText.Contains("quest3.pyro", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!AlvrSessionEditor.IsSafeDirectory(directory))
        {
            return false;
        }

        var name = Path.GetFileName(directory!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Contains("Pyrowave", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> DiscoverProfiles(string? sessionText)
    {
        if (string.IsNullOrWhiteSpace(sessionText))
        {
            return [];
        }

        return ResearchProfileNames.Where(name => sessionText.Contains(name, StringComparison.Ordinal)).ToList();
    }
}
