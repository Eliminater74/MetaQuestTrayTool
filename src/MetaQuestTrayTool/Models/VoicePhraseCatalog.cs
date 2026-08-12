namespace MetaQuestTrayTool.Models;

public sealed class VoicePhrase
{
    public required string Phrase { get; init; }
    public required HotKeyAction Action { get; init; }

    public string DescribeAction() => HotKeyCatalog.DescribeAction(Action);
}

public static class VoicePhraseCatalog
{
    public const int PushToTalkHotKeyId = 16383;

    public static IReadOnlyList<VoicePhrase> Phrases { get; } =
    [
        new VoicePhrase { Phrase = "apply global", Action = HotKeyAction.ApplyGlobal },
        new VoicePhrase { Phrase = "apply defaults", Action = HotKeyAction.ApplyGlobal },
        new VoicePhrase { Phrase = "restart service", Action = HotKeyAction.RestartOvrService },
        new VoicePhrase { Phrase = "restart o v r service", Action = HotKeyAction.RestartOvrService },
        new VoicePhrase { Phrase = "A S W off", Action = HotKeyAction.AswOff },
        new VoicePhrase { Phrase = "A S W auto", Action = HotKeyAction.AswAuto },
        new VoicePhrase { Phrase = "A S W forty five", Action = HotKeyAction.AswClock45 },
        new VoicePhrase { Phrase = "A S W forty-five", Action = HotKeyAction.AswClock45 },
        new VoicePhrase { Phrase = "cycle A S W", Action = HotKeyAction.AswCycle },
        new VoicePhrase { Phrase = "cycle supersampling", Action = HotKeyAction.SuperSamplingCycle },
        new VoicePhrase { Phrase = "cycle super sampling", Action = HotKeyAction.SuperSamplingCycle },
        new VoicePhrase { Phrase = "toggle H U D", Action = HotKeyAction.TogglePerfHud },
        new VoicePhrase { Phrase = "performance H U D", Action = HotKeyAction.TogglePerfHud }
    ];

    public static bool TryMatch(string recognizedText, out HotKeyAction action)
    {
        action = default;
        var normalized = Normalize(recognizedText);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var phrase in Phrases.OrderByDescending(phrase => Normalize(phrase.Phrase).Length))
        {
            var candidate = Normalize(phrase.Phrase);
            if (normalized.Equals(candidate, StringComparison.Ordinal)
                || normalized.Contains(candidate, StringComparison.Ordinal))
            {
                action = phrase.Action;
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string text) =>
        text.Trim().ToLowerInvariant()
            .Replace('.', ' ')
            .Replace('-', ' ');
}
