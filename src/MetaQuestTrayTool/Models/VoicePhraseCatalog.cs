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
        new VoicePhrase { Phrase = "dash to steam v r", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "start steam v r over link", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "steam v r from link", Action = HotKeyAction.DashToSteamVr },
        new VoicePhrase { Phrase = "start steam v r", Action = HotKeyAction.StartSteamVr },
        new VoicePhrase { Phrase = "launch steam v r", Action = HotKeyAction.StartSteamVr },
        new VoicePhrase { Phrase = "run steam v r", Action = HotKeyAction.StartSteamVr },
        new VoicePhrase { Phrase = "open steam v r", Action = HotKeyAction.StartSteamVr },
        new VoicePhrase { Phrase = "steam v r start", Action = HotKeyAction.StartSteamVr },
        new VoicePhrase { Phrase = "open steam v r home", Action = HotKeyAction.OpenSteamVrHome },
        new VoicePhrase { Phrase = "steam v r home", Action = HotKeyAction.OpenSteamVrHome },
        new VoicePhrase { Phrase = "launch steam v r home", Action = HotKeyAction.OpenSteamVrHome },
        new VoicePhrase { Phrase = "recover PCVR", Action = HotKeyAction.RecoverPcvr },
        new VoicePhrase { Phrase = "recover pcvr", Action = HotKeyAction.RecoverPcvr },
        new VoicePhrase { Phrase = "recover link", Action = HotKeyAction.RecoverPcvr },
        new VoicePhrase { Phrase = "recover PCVR session", Action = HotKeyAction.RecoverPcvr },
        new VoicePhrase { Phrase = "restore desktop audio", Action = HotKeyAction.RestoreDesktopAudio },
        new VoicePhrase { Phrase = "restore audio", Action = HotKeyAction.RestoreDesktopAudio },
        new VoicePhrase { Phrase = "switch to desktop audio", Action = HotKeyAction.RestoreDesktopAudio },
        new VoicePhrase { Phrase = "switch to VR audio", Action = HotKeyAction.SwitchToVrAudio },
        new VoicePhrase { Phrase = "switch to headset audio", Action = HotKeyAction.SwitchToVrAudio },
        new VoicePhrase { Phrase = "headset audio", Action = HotKeyAction.SwitchToVrAudio },
        new VoicePhrase { Phrase = "switch open x r meta", Action = HotKeyAction.OpenXrMeta },
        new VoicePhrase { Phrase = "open x r meta", Action = HotKeyAction.OpenXrMeta },
        new VoicePhrase { Phrase = "meta open x r", Action = HotKeyAction.OpenXrMeta },
        new VoicePhrase { Phrase = "switch open x r steam", Action = HotKeyAction.OpenXrSteamVr },
        new VoicePhrase { Phrase = "open x r steam", Action = HotKeyAction.OpenXrSteamVr },
        new VoicePhrase { Phrase = "open x r steam v r", Action = HotKeyAction.OpenXrSteamVr },
        new VoicePhrase { Phrase = "steam open x r", Action = HotKeyAction.OpenXrSteamVr },
        new VoicePhrase { Phrase = "close overlays", Action = HotKeyAction.CloseOverlays },
        new VoicePhrase { Phrase = "kill overlays", Action = HotKeyAction.CloseOverlays },
        new VoicePhrase { Phrase = "close overlay apps", Action = HotKeyAction.CloseOverlays },
        new VoicePhrase { Phrase = "apply GPU preset", Action = HotKeyAction.ApplyGpuPresets },
        new VoicePhrase { Phrase = "apply gpu preset", Action = HotKeyAction.ApplyGpuPresets },
        new VoicePhrase { Phrase = "apply GPU presets", Action = HotKeyAction.ApplyGpuPresets },
        new VoicePhrase { Phrase = "GPU preset", Action = HotKeyAction.ApplyGpuPresets }
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
