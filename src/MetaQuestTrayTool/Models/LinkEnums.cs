namespace MetaQuestTrayTool.Models;

public enum DistortionCurvature
{
    Default,
    Low,
    High
}

public enum EncodeDynamicBitrateMode
{
    Default,
    Disabled,
    Enabled
}

public enum LinkSharpeningMode
{
    Default,
    Disabled,
    Normal,
    Quality
}

public enum MobileAswMode
{
    Default,
    Disabled,
    Enabled
}

/// <summary>
/// Meta Link codec override. Registry evidence is limited to HEVC=1 (H.265) and
/// deleting HEVC (ODT shows Default). There is no observed DWORD that forces H.264.
/// </summary>
public enum LinkCodecMode
{
    /// <summary>Leave the HEVC value alone unless applying global defaults, which delete it.</summary>
    Default = 0,

    /// <summary>
    /// Request H.264 by deleting the HEVC override. This is the verified path to the
    /// ODT Default / H.264 behavior. It does not write HEVC=0.
    /// </summary>
    H264 = 1,

    /// <summary>Write the observed HEVC=1 override (H.265).</summary>
    Hevc = 2
}

/// <summary>
/// Sliced-encoding override. The only observed explicit value is NumSlices=1, which
/// ODT displayed as Off. No DWORD for Enabled was observed, so it is not offered.
/// </summary>
public enum SlicedEncodingMode
{
    /// <summary>Leave NumSlices alone unless applying global defaults, which delete it.</summary>
    Default = 0,

    /// <summary>Write NumSlices=1, observed to display Off in ODT.</summary>
    Disabled = 1
}

public static class LinkCodecModeLabels
{
    public const string Tooltip =
        "Select the Meta Link encoder preference. H.265 / HEVC writes the observed HEVC=1 override. H.264 removes that override. Deleting HEVC is the verified way ODT shows Default; an explicit DWORD that forces H.264 was not observed, so H.264 does not write HEVC=0.";

    public static string Describe(LinkCodecMode mode) => mode switch
    {
        LinkCodecMode.H264 => "H.264",
        LinkCodecMode.Hevc => "H.265 / HEVC",
        _ => "Default / Meta controlled"
    };
}

public static class SlicedEncodingModeLabels
{
    public const string Tooltip =
        "Default leaves sliced encoding to Meta and removes NumSlices only when applying defaults. Disabled writes NumSlices=1, which ODT displayed as Off. An explicit registry value that forces sliced encoding On was not observed, so Enabled is not offered.";

    public static string Describe(SlicedEncodingMode mode) => mode switch
    {
        SlicedEncodingMode.Disabled => "Disabled (NumSlices = 1)",
        _ => "Default / Meta controlled"
    };
}
