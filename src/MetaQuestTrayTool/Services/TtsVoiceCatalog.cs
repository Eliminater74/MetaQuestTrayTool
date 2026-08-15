using System.Globalization;
using System.Speech.Synthesis;

namespace MetaQuestTrayTool.Services;

/// <summary>Installed Windows TTS voices for headset announcements and voice-command confirmations.</summary>
public static class TtsVoiceCatalog
{
    /// <summary>Empty VoiceName means auto: prefer an English female voice.</summary>
    public const string AutoVoiceName = "";

    public static IReadOnlyList<InstalledTtsVoice> ListEnabled()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices()
                .Where(voice => voice.Enabled)
                .Select(voice => new InstalledTtsVoice(
                    voice.VoiceInfo.Name,
                    FormatDisplay(voice.VoiceInfo),
                    voice.VoiceInfo.Gender,
                    voice.VoiceInfo.Culture))
                .OrderBy(voice => voice.Gender == VoiceGender.Female ? 0 : 1)
                .ThenBy(voice => IsEnglish(voice.Culture) ? 0 : 1)
                .ThenBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string Apply(SpeechSynthesizer synthesizer, string? preferredName)
    {
        ArgumentNullException.ThrowIfNull(synthesizer);

        var installed = synthesizer.GetInstalledVoices().Where(voice => voice.Enabled).Select(voice => voice.VoiceInfo).ToList();
        if (installed.Count == 0)
        {
            return synthesizer.Voice.Name;
        }

        var pick = FindByName(installed, preferredName)
                   ?? PreferFemale(installed)
                   ?? installed[0];

        if (!string.Equals(synthesizer.Voice.Name, pick.Name, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                synthesizer.SelectVoice(pick.Name);
            }
            catch
            {
                return synthesizer.Voice.Name;
            }
        }

        return synthesizer.Voice.Name;
    }

    public static string DescribeActive(string? preferredName)
    {
        var applied = ListEnabled();
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            var auto = applied.FirstOrDefault(voice => voice.Gender == VoiceGender.Female && IsEnglish(voice.Culture))
                       ?? applied.FirstOrDefault(voice => voice.Gender == VoiceGender.Female)
                       ?? applied.FirstOrDefault();
            return auto is null ? "Auto (female preferred)" : $"Auto — {auto.Display}";
        }

        var named = applied.FirstOrDefault(voice =>
            voice.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
        return named?.Display ?? preferredName;
    }

    private static VoiceInfo? FindByName(IReadOnlyList<VoiceInfo> installed, string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            return null;
        }

        return installed.FirstOrDefault(voice =>
            voice.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
    }

    private static VoiceInfo? PreferFemale(IReadOnlyList<VoiceInfo> installed) =>
        installed.FirstOrDefault(voice => voice.Gender == VoiceGender.Female && IsEnglish(voice.Culture))
        ?? installed.FirstOrDefault(voice => voice.Gender == VoiceGender.Female)
        ?? installed.FirstOrDefault(voice =>
            voice.Name.Contains("Zira", StringComparison.OrdinalIgnoreCase));

    private static bool IsEnglish(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

    private static string FormatDisplay(VoiceInfo voice)
    {
        var gender = voice.Gender switch
        {
            VoiceGender.Female => "Female",
            VoiceGender.Male => "Male",
            VoiceGender.Neutral => "Neutral",
            _ => "Unknown"
        };
        var culture = string.IsNullOrWhiteSpace(voice.Culture.DisplayName)
            ? voice.Culture.Name
            : voice.Culture.DisplayName;
        return $"{voice.Name} — {gender} · {culture}";
    }
}

public sealed record InstalledTtsVoice(
    string Name,
    string Display,
    VoiceGender Gender,
    CultureInfo Culture);
