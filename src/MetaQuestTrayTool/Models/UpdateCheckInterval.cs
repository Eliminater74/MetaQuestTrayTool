namespace MetaQuestTrayTool.Models;

/// <summary>How often the tray checks GitHub for a newer release while running.</summary>
public enum UpdateCheckInterval
{
    /// <summary>Only check on start (if enabled) or when the user clicks Check now.</summary>
    Off = 0,
    Daily = 1,
    Every3Days = 2,
    Weekly = 3,
    Every2Weeks = 4,
    Monthly = 5
}

public static class UpdateCheckIntervalHelper
{
    public static string Describe(UpdateCheckInterval interval) => interval switch
    {
        UpdateCheckInterval.Off => "Off (only on start / Check now)",
        UpdateCheckInterval.Daily => "Every day",
        UpdateCheckInterval.Every3Days => "Every 3 days",
        UpdateCheckInterval.Weekly => "Every week",
        UpdateCheckInterval.Every2Weeks => "Every 2 weeks",
        UpdateCheckInterval.Monthly => "Every month",
        _ => interval.ToString()
    };

    public static TimeSpan? ToTimeSpan(UpdateCheckInterval interval) => interval switch
    {
        UpdateCheckInterval.Daily => TimeSpan.FromDays(1),
        UpdateCheckInterval.Every3Days => TimeSpan.FromDays(3),
        UpdateCheckInterval.Weekly => TimeSpan.FromDays(7),
        UpdateCheckInterval.Every2Weeks => TimeSpan.FromDays(14),
        UpdateCheckInterval.Monthly => TimeSpan.FromDays(30),
        _ => null
    };

    public static bool IsDue(UpdateCheckInterval interval, DateTimeOffset? lastCheckUtc, DateTimeOffset utcNow)
    {
        var period = ToTimeSpan(interval);
        if (period is null)
        {
            return false;
        }

        if (lastCheckUtc is null)
        {
            return true;
        }

        return utcNow - lastCheckUtc.Value >= period.Value;
    }
}
