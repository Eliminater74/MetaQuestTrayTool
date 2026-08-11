namespace MetaQuestTrayTool.Models;

/// <summary>
/// Runtime values pushed through OculusDebugToolCLI / stored as global defaults.
/// SuperSampling 0 means "do not override" (same as original OTT).
/// </summary>
public sealed class GameSettings
{
    public static readonly double[] SuperSamplingPresets =
    [
        0,
        1.0,
        1.1,
        1.2,
        1.3,
        1.4,
        1.5,
        1.7,
        2.0
    ];

    public double SuperSampling { get; set; } = 1.0;
    public AswMode AswMode { get; set; } = AswMode.Auto;
    public double FovMultiplierHorizontal { get; set; } = 1.0;
    public double FovMultiplierVertical { get; set; } = 1.0;

    /// <summary>Backward-compatible single FOV value used by older settings files.</summary>
    public double FovMultiplier
    {
        get => FovMultiplierHorizontal;
        set
        {
            FovMultiplierHorizontal = value;
            FovMultiplierVertical = value;
        }
    }

    public bool AdaptiveGpuScaling { get; set; } = true;
    public string OvrServerPriority { get; set; } = "Normal";
    public bool ForceMipMapOnLayers { get; set; }
    public double OffsetMipMapOnLayers { get; set; }
    public bool UseFovStencil { get; set; } = true;
    public VisualHudMode VisualHud { get; set; } = VisualHudMode.None;

    public GameSettings Clone() => new()
    {
        SuperSampling = SuperSampling,
        AswMode = AswMode,
        FovMultiplierHorizontal = FovMultiplierHorizontal,
        FovMultiplierVertical = FovMultiplierVertical,
        AdaptiveGpuScaling = AdaptiveGpuScaling,
        OvrServerPriority = OvrServerPriority,
        ForceMipMapOnLayers = ForceMipMapOnLayers,
        OffsetMipMapOnLayers = OffsetMipMapOnLayers,
        UseFovStencil = UseFovStencil,
        VisualHud = VisualHud
    };

    public string Describe()
    {
        var ss = SuperSampling <= 0 ? "off" : SuperSampling.ToString("0.0");
        var fov = $"{FovMultiplierHorizontal:0.00}/{FovMultiplierVertical:0.00}";
        return $"SS {ss}, ASW {AswMode}, FOV {fov}";
    }
}
