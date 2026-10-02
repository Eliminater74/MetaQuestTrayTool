using System.Text.Json.Serialization;

namespace MetaQuestTrayTool.Models;

/// <summary>
/// Quest Link / Air Link streaming overrides.
/// Numeric 0 / Default enum values mean "do not override".
/// </summary>
public sealed class LinkSettings : IJsonOnDeserialized
{
    public const int LegacyBitratePresetCeilingMbps = 500;

    public static readonly int[] BitratePresets =
    [
        0, 50, 75, 100, 150, 200, 250, 300, 350, 400,
        LegacyBitratePresetCeilingMbps, 600, 700, 800, 900, 960
    ];

    public static readonly int[] EncodeWidthPresets =
    [
        0, 2016, 2352, 2608, 2912, 3136, 3664, 4128
    ];

    public string PresetName { get; set; } = "Custom";
    public DistortionCurvature DistortionCurvature { get; set; } = DistortionCurvature.Default;
    public int EncodeResolutionWidth { get; set; }
    public int BitrateMbps { get; set; }
    public EncodeDynamicBitrateMode EncodeDynamicBitrate { get; set; } = EncodeDynamicBitrateMode.Default;
    public int DynamicBitrateMax { get; set; }
    public LinkSharpeningMode Sharpening { get; set; } = LinkSharpeningMode.Default;

    private LinkCodecMode _codec = LinkCodecMode.Default;
    private bool _codecSpecified;
    private bool? _legacyPreferHevc;

    public LinkCodecMode Codec
    {
        get => _codec;
        set
        {
            _codec = value;
            _codecSpecified = true;
        }
    }

    private SlicedEncodingMode _slicedEncoding = SlicedEncodingMode.Default;
    private bool _slicedSpecified;
    private bool? _legacyDisableSlicedEncoding;

    public SlicedEncodingMode SlicedEncoding
    {
        get => _slicedEncoding;
        set
        {
            _slicedEncoding = value;
            _slicedSpecified = true;
        }
    }

    /// <summary>
    /// Old checkbox. True still selects HEVC. False clears an HEVC selection back to
    /// Default and does not invent an explicit H.264 choice.
    /// Not serialized; <see cref="LegacyPreferHevc"/> reads old settings.json files.
    /// </summary>
    [JsonIgnore]
    public bool PreferHevc
    {
        get => Codec == LinkCodecMode.Hevc;
        set => Codec = value
            ? LinkCodecMode.Hevc
            : Codec == LinkCodecMode.Hevc ? LinkCodecMode.Default : Codec;
    }

    /// <summary>
    /// Old checkbox. True selects Disabled (NumSlices=1). False clears Disabled back to Default.
    /// </summary>
    [JsonIgnore]
    public bool DisableSlicedEncoding
    {
        get => SlicedEncoding == SlicedEncodingMode.Disabled;
        set => SlicedEncoding = value
            ? SlicedEncodingMode.Disabled
            : SlicedEncoding == SlicedEncodingMode.Disabled ? SlicedEncodingMode.Default : SlicedEncoding;
    }

    /// <summary>
    /// Deserializes the pre-mode "PreferHevc" field. Always omitted on save so a later
    /// explicit codec choice is not overwritten by the old bool.
    /// </summary>
    [JsonPropertyName("PreferHevc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LegacyPreferHevc
    {
        get => false;
        set => _legacyPreferHevc = value;
    }

    /// <summary>
    /// Deserializes the pre-mode "DisableSlicedEncoding" field. Always omitted on save.
    /// </summary>
    [JsonPropertyName("DisableSlicedEncoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LegacyDisableSlicedEncoding
    {
        get => false;
        set => _legacyDisableSlicedEncoding = value;
    }

    public int DynamicBitrateOffsetMbps { get; set; }
    public MobileAswMode MobileAsw { get; set; } = MobileAswMode.Default;

    public void OnDeserialized()
    {
        if (!_codecSpecified && _legacyPreferHevc == true)
        {
            _codec = LinkCodecMode.Hevc;
        }

        if (!_slicedSpecified && _legacyDisableSlicedEncoding == true)
        {
            _slicedEncoding = SlicedEncodingMode.Disabled;
        }
    }

    public LinkSettings Clone() => new()
    {
        PresetName = PresetName,
        DistortionCurvature = DistortionCurvature,
        EncodeResolutionWidth = EncodeResolutionWidth,
        BitrateMbps = BitrateMbps,
        EncodeDynamicBitrate = EncodeDynamicBitrate,
        DynamicBitrateMax = DynamicBitrateMax,
        Sharpening = Sharpening,
        Codec = Codec,
        SlicedEncoding = SlicedEncoding,
        DynamicBitrateOffsetMbps = DynamicBitrateOffsetMbps,
        MobileAsw = MobileAsw
    };

    public string Describe()
    {
        var bitrate = BitrateMbps <= 0 ? "default" : $"{BitrateMbps} Mbps";
        var width = EncodeResolutionWidth <= 0 ? "auto" : EncodeResolutionWidth.ToString();
        var distortion = DistortionCurvature == DistortionCurvature.Default
            ? "distortion default"
            : $"distortion {DistortionCurvature}";
        var dbrOffset = DynamicBitrateOffsetMbps == 0
            ? string.Empty
            : $", DBR offset {DynamicBitrateOffsetMbps} Mbps";
        var mobileAsw = MobileAsw == MobileAswMode.Default ? string.Empty : $", mobile ASW {MobileAsw}";
        var codec = Codec switch
        {
            LinkCodecMode.Hevc => "codec HEVC",
            LinkCodecMode.H264 => "codec H.264 (HEVC override removed)",
            _ => "codec default"
        };
        var slices = SlicedEncoding == SlicedEncodingMode.Disabled
            ? "sliced off (NumSlices=1)"
            : "sliced default";
        return $"Preset {PresetName}, Bitrate {bitrate}, Encode {width}, {EncodeDynamicBitrate}, DBR max {(DynamicBitrateMax <= 0 ? "auto" : DynamicBitrateMax.ToString())}{dbrOffset}, {distortion}, Sharpen {Sharpening}, {codec}, {slices}{mobileAsw}";
    }
}
