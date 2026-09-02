using System.Diagnostics;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Optionally ends overlay / RGB / OSD processes when a PCVR session connects.
/// Never kills SteamVR, Oculus, or Discord — only the configured overlay list.
/// </summary>
public sealed class OverlayCloseService
{
    private static readonly HashSet<string> NeverKill = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamwebhelper", "steamvr", "vrserver", "vrdashboard", "vrcompositor",
        "ovrserver_x64", "oculusclient", "oculusdash", "oculusvr", "meta",
        "discord", "explorer", "csrss", "winlogon", "services", "system",
        "metaquesttraytool", "dwm"
    };

    private readonly App _app;

    public OverlayCloseService(App app) => _app = app;

    public string CloseConfiguredOverlays(string reason = "Link connect", bool force = false)
    {
        var settings = _app.Settings.Current;
        if (!force && !settings.CloseOverlaysOnLinkConnect)
        {
            return string.Empty;
        }

        var targets = (settings.OverlayCloseProcesses ?? [])
            .Select(ProfileService.NormalizeProcessName)
            .Where(name => name.Length > 0 && !NeverKill.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            return string.Empty;
        }

        var closed = new List<string>();
        var forced = new List<string>();
        foreach (var name in targets)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        try
                        {
                            process.CloseMainWindow();
                            if (!process.WaitForExit(1500))
                            {
                                process.Kill(entireProcessTree: false);
                                forced.Add(name);
                            }

                            closed.Add(name);
                        }
                        catch
                        {
                            try
                            {
                                process.Kill(entireProcessTree: false);
                                forced.Add(name);
                                closed.Add(name);
                            }
                            catch
                            {
                                // Access denied / already exiting
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore per-name failures
            }
        }

        if (closed.Count == 0)
        {
            return string.Empty;
        }

        var unique = closed.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var forcedUnique = forced.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var forcedSummary = forcedUnique.Count == 0
            ? string.Empty
            : $" Forced termination was required for: {string.Join(", ", forcedUnique)}; child processes were left running.";
        var summary = $"Closed overlays on {reason}: {string.Join(", ", unique)}.{forcedSummary}";
        _app.Log.Info(summary);
        return summary;
    }
}
