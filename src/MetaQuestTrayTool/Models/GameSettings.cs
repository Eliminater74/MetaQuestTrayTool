namespace MetaQuestTrayTool.Models;

/// <summary>
/// Runtime values pushed through OculusDebugToolCLI.
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
    public double FovMultiplier { get; set; } = 1.0;

    public GameSettings Clone() => new()
    {
        SuperSampling = SuperSampling,
        AswMode = AswMode,
        FovMultiplier = FovMultiplier
    };

    public string Describe()
    {
        var ss = SuperSampling <= 0 ? "off" : SuperSampling.ToString("0.0");
        var fov = FovMultiplier.ToString("0.00");
        return $"SS {ss}, ASW {AswMode}, FOV {fov}";
    }
}
