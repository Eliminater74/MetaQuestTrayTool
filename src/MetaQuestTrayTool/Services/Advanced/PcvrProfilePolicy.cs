using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class PcvrProfilePolicy
{
    public static string Describe(Models.PcvrProfilePreferences? preferences)
    {
        if (preferences is null)
        {
            return "No provider preference.";
        }

        if (preferences.AutoSwitchProvider)
        {
            return "Auto-switch is ignored. Profiles do not launch or switch streaming providers.";
        }

        if (preferences.ExperimentalRefreshHz is 240 or 165 or 180 or 200)
        {
            return "The stored experimental refresh is not applied. That rate is not an exposed control.";
        }

        var provider = string.IsNullOrWhiteSpace(preferences.PreferredProvider) ? "unspecified" : preferences.PreferredProvider.Trim();
        return "Provider preference recorded for " + provider + ". It is not launched automatically.";
    }

    public static ProfileStepResult Decide(PcvrProfilePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.AutoSwitchProvider)
        {
            return new ProfileStepResult(
                "PCVR",
                ProfileStepStatus.Succeeded,
                "Auto-switch is stored but was ignored. No streaming provider was launched or stopped.");
        }

        if (preferences.ExperimentalRefreshHz is 240 or 165 or 180 or 200)
        {
            return new ProfileStepResult(
                "PCVR",
                ProfileStepStatus.Succeeded,
                "The experimental refresh on this profile was not written. That rate is not an exposed control, and no provider was launched.");
        }

        var notes = new List<string>
        {
            "Provider preference was evaluated. No streaming provider was launched and no session file was edited."
        };
        if (!string.IsNullOrWhiteSpace(preferences.PreferredProvider))
        {
            notes.Add("Preferred provider '" + preferences.PreferredProvider.Trim() + "' was not started. Settings apply only when that provider is already active.");
        }

        if (preferences.ExperimentalGpuMaximum == true)
        {
            notes.Add("GPU level 7 stays under the Headset page opt-in and the battery-temperature gate. This profile did not write it.");
        }

        if (preferences.ExperimentalRefreshHz is 144 or 207)
        {
            notes.Add("Experimental refresh stays under the Headset page opt-in. This profile did not write a refresh property.");
        }

        if (!string.IsNullOrWhiteSpace(preferences.AlvrProfile) || !string.IsNullOrWhiteSpace(preferences.PyrowaveProfile))
        {
            notes.Add("Named ALVR or Pyrowave presets were not applied.");
        }

        return new ProfileStepResult("PCVR", ProfileStepStatus.Succeeded, string.Join(" ", notes));
    }
}

public static class DiagnosticReportBuilder
{
    public static string Build(string body, string? serial)
    {
        var text = body ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(serial))
        {
            text = text.Replace(serial, "<serial>", StringComparison.Ordinal);
        }

        text += Environment.NewLine + "OpenVR dashboard overlay: not loaded.";
        text += Environment.NewLine + "240 Hz display scaling: not written.";
        text += Environment.NewLine + "GPU level 7 clock: not measured by this report.";
        return text.Trim();
    }
}
