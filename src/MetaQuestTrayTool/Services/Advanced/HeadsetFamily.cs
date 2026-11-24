namespace MetaQuestTrayTool.Services;

public enum HeadsetFamily
{
    Unknown,
    Quest1,
    Quest2,
    QuestPro,
    Quest3,
    Quest3S
}

public enum CapabilityState
{
    Verified,
    DocumentedModel,
    Experimental,
    Unsupported,
    Unknown,
    TemporarilyUnavailable
}

/// <summary>
/// Classifies a Quest model string. Quest 3S is matched before Quest 3 so experimental
/// Quest 3-only behavior cannot leak through the shared "Quest 3" substring.
/// </summary>
public static class HeadsetFamilyClassifier
{
    public static HeadsetFamily Classify(string? model)
    {
        var name = Normalize(model);
        if (name.Length == 0)
        {
            return HeadsetFamily.Unknown;
        }

        if (name.Contains("3S", StringComparison.OrdinalIgnoreCase))
        {
            return HeadsetFamily.Quest3S;
        }

        if (name.Contains("Quest Pro", StringComparison.OrdinalIgnoreCase))
        {
            return HeadsetFamily.QuestPro;
        }

        if (name.Contains("Quest 3", StringComparison.OrdinalIgnoreCase))
        {
            return HeadsetFamily.Quest3;
        }

        if (name.Contains("Quest 2", StringComparison.OrdinalIgnoreCase))
        {
            return HeadsetFamily.Quest2;
        }

        if (name.Equals("Quest", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Oculus Quest", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Meta Quest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Quest 1", StringComparison.OrdinalIgnoreCase))
        {
            return HeadsetFamily.Quest1;
        }

        return HeadsetFamily.Unknown;
    }

    public static string Normalize(string? model) => (model ?? string.Empty).Replace('_', ' ').Trim();
}
