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
        new VoicePhrase { Phrase = "performance H U D", Action = HotKeyAction.TogglePerfHud },
        new VoicePhrase { Phrase = "open meta link", Action = HotKeyAction.OpenMetaLink },
        new VoicePhrase { Phrase = "open meta horizon link", Action = HotKeyAction.OpenMetaLink },
        new VoicePhrase { Phrase = "show meta link", Action = HotKeyAction.OpenMetaLink },
        new VoicePhrase { Phrase = "open oculus client", Action = HotKeyAction.OpenMetaLink },
        new VoicePhrase { Phrase = "open debug tool", Action = HotKeyAction.OpenDebugTool },
        new VoicePhrase { Phrase = "open oculus debug tool", Action = HotKeyAction.OpenDebugTool },
        new VoicePhrase { Phrase = "launch debug tool", Action = HotKeyAction.OpenDebugTool },
        new VoicePhrase { Phrase = "kill dash", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "start steam v r", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "dash to steam v r", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "steam v r from link", Action = HotKeyAction.DashToSteamVr }
    ];

    public static bool TryMatch(string recognizedText, out HotKeyAction action) =>
        TryMatch(recognizedText, extraPhrases: null, out action);

    public static bool TryMatch(
        string recognizedText,
        IEnumerable<VoiceCustomPhrase>? extraPhrases,
        out HotKeyAction action)
    {
        action = default;
        var normalized = Normalize(recognizedText);
        if (normalized.Length == 0)
        {
            return false;
        }

        var catalog = Phrases
            .Select(phrase => (phrase.Phrase, phrase.Action))
            .Concat((extraPhrases ?? [])
                .Where(phrase => !string.IsNullOrWhiteSpace(phrase.Phrase))
                .Select(phrase => (phrase.Phrase, phrase.Action)));

        foreach (var (phrase, mapped) in catalog.OrderByDescending(entry => Normalize(entry.Phrase).Length))
        {
            var candidate = Normalize(phrase);
            if (normalized.Equals(candidate, StringComparison.Ordinal)
                || normalized.Contains(candidate, StringComparison.Ordinal))
            {
                action = mapped;
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
