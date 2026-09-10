using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class HeadsetCapabilities
{
    public static IReadOnlyList<HeadsetRefreshRate> RefreshRates(string? model)
    {
        var name = (model ?? "").Replace('_', ' ').Trim();
        if (name.Contains("Quest Pro", StringComparison.OrdinalIgnoreCase))
            return [HeadsetRefreshRate.DeviceDefault, HeadsetRefreshRate.Hz72, HeadsetRefreshRate.Hz80, HeadsetRefreshRate.Hz90];
        if (name.Contains("Quest 3", StringComparison.OrdinalIgnoreCase))
            return [HeadsetRefreshRate.DeviceDefault, HeadsetRefreshRate.Hz72, HeadsetRefreshRate.Hz80, HeadsetRefreshRate.Hz90, HeadsetRefreshRate.Hz120];
        if (name.Contains("Quest 2", StringComparison.OrdinalIgnoreCase))
            return Enum.GetValues<HeadsetRefreshRate>();
        if (name.Equals("Quest", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Oculus Quest", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Meta Quest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Quest 1", StringComparison.OrdinalIgnoreCase))
            return [HeadsetRefreshRate.DeviceDefault, HeadsetRefreshRate.Hz60, HeadsetRefreshRate.Hz72];
        return [HeadsetRefreshRate.DeviceDefault];
    }
}
