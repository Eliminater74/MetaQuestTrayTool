using System.Globalization;
using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public sealed record ExperimentalRefreshMode(int Hz, string Panel, bool CanApply, string Reason);

public sealed class ExperimentalRefreshDecision
{
    public bool Apply { get; init; }
    public int? Hz { get; init; }
    public string? Refusal { get; init; }
    public string? Warning { get; init; }
}

public enum PanelEvidence
{
    NotRead,
    Unparsed,
    Matches,
    Differs
}

public sealed class RefreshVerification
{
    public int RequestedHz { get; init; }
    public string? PropertyReadback { get; init; }
    public bool PropertyMatched { get; init; }
    public double? EffectivePanelHz { get; init; }
    public PanelEvidence Panel { get; init; }
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Quest 3 panel research from Quest3-Pyrowave docs/HIGH-REFRESH.md at
/// 1b529ef54b4f7b34a44c28ad9a54c6c4ac6887a7. 144 and 207 are property requests only.
/// 240 Hz scaling is not written.
/// </summary>
public static class ExperimentalRefreshPolicy
{
    public const string RefreshProperty = "debug.oculus.refreshRate";
    public const string ScalingProperty = "debug.oculus.forceDisplayScaling";
    private static readonly Regex PanelRate = new(
        @"(?i)\b(?:mCurrentRefreshRate|refreshRate|refresh\s*rate)\s*[=:]\s*(?<n>\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    public static IReadOnlyList<ExperimentalRefreshMode> ModesFor(HeadsetFamily family)
    {
        if (family != HeadsetFamily.Quest3)
        {
            return
            [
                new ExperimentalRefreshMode(144, "not offered", false, "Experimental panel rates are not offered unless the model is Quest 3 and not Quest 3S."),
                new ExperimentalRefreshMode(207, "not offered", false, "Experimental panel rates are not offered unless the model is Quest 3 and not Quest 3S."),
                new ExperimentalRefreshMode(240, "scaled research", false, "240 Hz requires display scaling and a kernel mode check this tray does not perform.")
            ];
        }

        return
        [
            new ExperimentalRefreshMode(144, "native research", true, "Writes debug.oculus.refreshRate=144. This does not make Quest Link deliver 144 FPS."),
            new ExperimentalRefreshMode(207, "native research", true, "Writes debug.oculus.refreshRate=207. This does not make a streaming app deliver 207 FPS."),
            new ExperimentalRefreshMode(165, "not established", false, "165 Hz was not a confirmed panel mode in the researched document."),
            new ExperimentalRefreshMode(180, "not established", false, "180 Hz was not a confirmed panel mode in the researched document."),
            new ExperimentalRefreshMode(200, "not established", false, "200 Hz was not a confirmed panel mode in the researched document."),
            new ExperimentalRefreshMode(240, "scaled research", false, "240 Hz needs debug.oculus.forceDisplayScaling and a confirmed kernel mode. This tray does not write that property.")
        ];
    }

    public static ExperimentalRefreshDecision Evaluate(bool optedIn, string? model, int? requestedHz, bool standardRefreshAlsoSet)
    {
        if (!optedIn || requestedHz is null or 0)
        {
            return new ExperimentalRefreshDecision();
        }

        if (standardRefreshAlsoSet)
        {
            return new ExperimentalRefreshDecision
            {
                Refusal = "Set the standard refresh box to device default before requesting an experimental rate."
            };
        }

        var family = HeadsetFamilyClassifier.Classify(model);
        var mode = ModesFor(family).FirstOrDefault(item => item.Hz == requestedHz);
        if (mode is not { CanApply: true })
        {
            return new ExperimentalRefreshDecision
            {
                Refusal = mode?.Reason ?? $"{requestedHz.Value.ToString(CultureInfo.InvariantCulture)} Hz is not an exposed experimental rate."
            };
        }

        return new ExperimentalRefreshDecision
        {
            Apply = true,
            Hz = mode.Hz,
            Warning = mode.Reason + " Hardware verification is pending. The previous property is snapshotted. A property readback is not an effective panel rate, an OpenXR rate, a streaming frame rate, or fresh-frame FPS."
        };
    }

    public static RefreshVerification Interpret(int requestedHz, string? propertyReadback, string? displayDump)
    {
        var requestedText = requestedHz.ToString(CultureInfo.InvariantCulture);
        var propertyMatched = string.Equals(propertyReadback?.Trim(), requestedText, StringComparison.Ordinal);
        double? panel = null;
        var evidence = PanelEvidence.NotRead;
        if (displayDump is not null)
        {
            var rates = PanelRate.Matches(displayDump)
                .Select(match => Math.Round(double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture), 1))
                .Distinct()
                .ToList();
            if (rates.Count == 1)
            {
                panel = rates[0];
                evidence = Math.Abs(panel.Value - requestedHz) < 0.6 ? PanelEvidence.Matches : PanelEvidence.Differs;
            }
            else
            {
                evidence = PanelEvidence.Unparsed;
            }
        }

        var panelText = evidence switch
        {
            PanelEvidence.NotRead => "not read",
            PanelEvidence.Unparsed => "not parsed from the display dump",
            PanelEvidence.Matches => panel?.ToString("0.#", CultureInfo.InvariantCulture) + " Hz, matches the requested property",
            _ => panel?.ToString("0.#", CultureInfo.InvariantCulture) + " Hz, differs from the requested property"
        };
        var summary = string.Join(Environment.NewLine, [
            $"Requested property: {RefreshProperty}={requestedText}",
            $"Property readback: {propertyReadback ?? "not read"}",
            $"Effective panel rate: {panelText}",
            "OpenXR application rate: not measured",
            "Streaming frame rate: not measured",
            "Fresh delivered FPS: not measured",
            evidence == PanelEvidence.Matches
                ? "The display dump agrees with the requested property. This is still not a streaming frame rate."
                : "The property readback is not evidence that the panel, OpenXR runtime, or stream changed."
        ]);
        return new RefreshVerification
        {
            RequestedHz = requestedHz,
            PropertyReadback = propertyReadback,
            PropertyMatched = propertyMatched,
            EffectivePanelHz = panel,
            Panel = evidence,
            Summary = summary
        };
    }
}
