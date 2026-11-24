namespace MetaQuestTrayTool.Overlay;

public enum OverlayDisplayMode
{
    Compact,
    Detail,
    Hidden
}

public static class OverlayInteraction
{
    public const string PipeName = "MetaQuestTrayTool.PerformanceOverlay";

    public static string AuthLine(string token, string command)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("The overlay token is missing or contains whitespace.");
        }

        if (string.IsNullOrWhiteSpace(command) || command.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("The overlay command is empty or contains a line break.");
        }

        return "auth " + token.Trim() + " " + command.Trim();
    }

    public static bool IsBenchmarkStop(float x, float y) => y >= 0.75f && x >= 0.5f;

    public static OverlayDisplayMode NextMode(float x, float y, OverlayDisplayMode current)
    {
        if (y < 0.75f || x >= 0.5f)
        {
            return current;
        }

        return current switch
        {
            OverlayDisplayMode.Compact => OverlayDisplayMode.Detail,
            OverlayDisplayMode.Detail => OverlayDisplayMode.Hidden,
            _ => OverlayDisplayMode.Compact
        };
    }

    public static string FindSteamVrApi()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var candidate = Path.Combine(root, "Steam", "steamapps", "common", "SteamVR", "bin", "win64", "openvr_api.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
