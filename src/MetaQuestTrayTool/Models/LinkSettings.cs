namespace MetaQuestTrayTool.Models;

/// <summary>
/// Quest Link / Air Link streaming overrides persisted under
/// HKCU\Software\Oculus\RemoteHeadset (same place the Meta runtime reads).
/// 0 means "do not override / use Meta default" for numeric fields.
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

    /// <summary>Encode bitrate in Mbps. 0 = Meta default.</summary>
    public int BitrateMbps { get; set; }

    /// <summary>Encode resolution width. 0 = Meta default / auto.</summary>
    public int EncodeResolutionWidth { get; set; }

    /// <summary>Prefer HEVC/H.265 when the runtime supports it.</summary>
    public bool PreferHevc { get; set; }

    /// <summary>Disable sliced encoding (helps some wired Link artifacts).</summary>
    public bool DisableSlicedEncoding { get; set; }

    public LinkSettings Clone() => new()
    {
        BitrateMbps = BitrateMbps,
        EncodeResolutionWidth = EncodeResolutionWidth,
        PreferHevc = PreferHevc,
        DisableSlicedEncoding = DisableSlicedEncoding
    };

    public string Describe()
    {
        var bitrate = BitrateMbps <= 0 ? "default" : $"{BitrateMbps} Mbps";
        var width = EncodeResolutionWidth <= 0 ? "auto" : EncodeResolutionWidth.ToString();
        var codec = PreferHevc ? "HEVC" : "H.264";
        var slices = DisableSlicedEncoding ? "slices off" : "slices default";
        return $"Bitrate {bitrate}, Encode {width}, {codec}, {slices}";
    }
}
