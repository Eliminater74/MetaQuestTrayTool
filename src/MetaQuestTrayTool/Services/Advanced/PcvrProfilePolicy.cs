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
