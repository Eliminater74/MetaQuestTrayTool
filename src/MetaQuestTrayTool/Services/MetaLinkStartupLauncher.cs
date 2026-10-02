namespace MetaQuestTrayTool.Services;

/// <summary>
/// One startup attempt to open Meta Horizon Link. Client discovery and the
/// unelevated session-helper launch stay in <see cref="OculusRuntimeService.ShowMetaHorizonLink"/>.
/// </summary>
public static class MetaLinkStartupLauncher
{
    public static void LaunchIfEnabled(bool enabled, Func<string> open, Action<string> info, Action<string> warn)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            var message = open();
            if (IsFailure(message))
            {
                warn(string.IsNullOrWhiteSpace(message)
                    ? "Could not open Meta Horizon Link on startup."
                    : message);
                return;
            }

            info(message);
        }
        catch (Exception ex)
        {
            warn("Could not open Meta Horizon Link on startup: " + ex.Message);
        }
    }

    internal static bool IsFailure(string? message) =>
        string.IsNullOrWhiteSpace(message)
        || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Could not", StringComparison.OrdinalIgnoreCase);
}
