namespace MetaQuestTrayTool.Models;

/// <summary>
/// Optional Link / Air Link overrides on a personal profile.
/// Default / null means inherit the global Quest Link page values.
/// </summary>
public sealed class LinkProfileOverrides
{
    public LinkSharpeningMode Sharpening { get; set; } = LinkSharpeningMode.Default;
    public int? BitrateMbps { get; set; }
    public int? EncodeResolutionWidth { get; set; }

    public bool HasAny =>
        Sharpening != LinkSharpeningMode.Default
        || BitrateMbps is not null
        || EncodeResolutionWidth is not null;

    public LinkSettings Overlay(LinkSettings globals)
    {
        var merged = globals.Clone();
        if (Sharpening != LinkSharpeningMode.Default)
        {
            merged.Sharpening = Sharpening;
        }

        if (BitrateMbps is int bitrate)
        {
            merged.BitrateMbps = bitrate;
        }

        if (EncodeResolutionWidth is int width)
        {
            merged.EncodeResolutionWidth = width;
        }

        return merged;
    }

    public string Describe()
    {
        if (!HasAny)
        {
            return "Link inherit";
        }

        var parts = new List<string>();
        if (Sharpening != LinkSharpeningMode.Default)
        {
            parts.Add($"sharpen {Sharpening}");
        }

        if (BitrateMbps is int bitrate)
        {
            parts.Add(bitrate <= 0 ? "bitrate default" : $"{bitrate} Mbps");
        }

        if (EncodeResolutionWidth is int width)
        {
            parts.Add(width <= 0 ? "encode auto" : $"encode {width}");
        }

        return "Link " + string.Join(", ", parts);
    }
}
