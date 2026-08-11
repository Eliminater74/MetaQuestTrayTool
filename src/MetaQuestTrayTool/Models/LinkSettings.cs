namespace MetaQuestTrayTool.Models;

/// <summary>
/// Quest Link / Air Link streaming overrides.
/// Numeric 0 / Default enum values mean "do not override".
/// </summary>
public sealed class LinkSettings
{
    public static readonly int[] BitratePresets =
    [
        0, 50, 75, 100, 150, 200, 250, 300, 350, 400, 500
    ];

    public static readonly int[] EncodeWidthPresets =
    [
        0, 2016, 2352, 2608, 2912, 3136, 3664
    ];

    public string PresetName { get; set; } = "Custom";
    public DistortionCurvature DistortionCurvature { get; set; } = DistortionCurvature.Default;
    public int EncodeResolutionWidth { get; set; }
    public int BitrateMbps { get; set; }
    public EncodeDynamicBitrateMode EncodeDynamicBitrate { get; set; } = EncodeDynamicBitrateMode.Default;
    public int DynamicBitrateMax { get; set; }
    public LinkSharpeningMode Sharpening { get; set; } = LinkSharpeningMode.Default;
    public bool PreferHevc { get; set; }
    public bool DisableSlicedEncoding { get; set; }

    public LinkSettings Clone() => new()
    {
        PresetName = PresetName,
        DistortionCurvature = DistortionCurvature,
        EncodeResolutionWidth = EncodeResolutionWidth,
        BitrateMbps = BitrateMbps,
        EncodeDynamicBitrate = EncodeDynamicBitrate,
        DynamicBitrateMax = DynamicBitrateMax,
        Sharpening = Sharpening,
        PreferHevc = PreferHevc,
        DisableSlicedEncoding = DisableSlicedEncoding
    };

    public string Describe()
    {
        var bitrate = BitrateMbps <= 0 ? "default" : $"{BitrateMbps} Mbps";
        var width = EncodeResolutionWidth <= 0 ? "auto" : EncodeResolutionWidth.ToString();
        return $"Preset {PresetName}, Bitrate {bitrate}, Encode {width}, {EncodeDynamicBitrate}, Sharpen {Sharpening}";
    }
}
