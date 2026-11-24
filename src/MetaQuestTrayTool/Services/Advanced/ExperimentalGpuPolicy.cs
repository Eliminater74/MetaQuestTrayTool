using System.Globalization;

namespace MetaQuestTrayTool.Services;

public sealed class ExperimentalGpuDecision
{
    public bool ApplyLevel7 { get; init; }
    public string? Refusal { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Quest 3 debug.oculus.gpuLevel=7, as documented by Quest3-Pyrowave tools/quest3/gpu_level.py
/// at commit 1b529ef54b4f7b34a44c28ad9a54c6c4ac6887a7. Their short screens associated level 4
/// with 640 MHz and level 7 with 690 MHz. This policy does not treat a property write as a clock measurement.
/// Levels 5 and 6 are not exposed.
/// </summary>
public static class ExperimentalGpuPolicy
{
    public const int MaximumLevel = 7;
    public const string PropertyName = "debug.oculus.gpuLevel";
    public const double RefuseAtOrAboveCelsius = 45;

    public static ExperimentalGpuDecision Evaluate(bool optedIn, string? model, double? batteryTemperatureC)
    {
        if (!optedIn)
        {
            return new ExperimentalGpuDecision();
        }

        var family = HeadsetFamilyClassifier.Classify(model);
        var state = HeadsetCapabilityDiscovery.GpuLevel7State(family);
        if (state != CapabilityState.Experimental)
        {
            return new ExperimentalGpuDecision
            {
                Refusal = $"Experimental GPU level 7 is {state.ToString().ToLowerInvariant()} for '{model ?? "unknown"}'. It was not written."
            };
        }

        if (batteryTemperatureC is double temperature && temperature >= RefuseAtOrAboveCelsius)
        {
            return new ExperimentalGpuDecision
            {
                Refusal = $"Battery temperature is {temperature.ToString("0.0", CultureInfo.InvariantCulture)} C, which is at or above the {RefuseAtOrAboveCelsius.ToString("0", CultureInfo.InvariantCulture)} C application gate. GPU level 7 was not written. Firmware thermal limits are unchanged."
            };
        }

        var warning = batteryTemperatureC is null
            ? "Battery temperature was not available, so the 45 C gate was not evaluated. Firmware thermal limits still apply."
            : $"Battery temperature was {batteryTemperatureC.Value.ToString("0.0", CultureInfo.InvariantCulture)} C.";
        return new ExperimentalGpuDecision
        {
            ApplyLevel7 = true,
            Warning = warning + " Property readback does not prove the GPU clock. Hardware verification is pending. Sustained thermals are not established."
        };
    }

    public static string DescribeObservation(int? requestedLevel, string? propertyReadback, int? runtimeLevel, double? clockMHz, double? utilizationPercent, string? thermal)
    {
        return string.Join(Environment.NewLine, [
            $"Requested GPU level: {(requestedLevel is int requested ? requested.ToString(CultureInfo.InvariantCulture) : "not requested")}",
            $"Property readback: {propertyReadback ?? "not read"}",
            $"Runtime-reported GPU level: {(runtimeLevel is int runtime ? runtime.ToString(CultureInfo.InvariantCulture) : "not in this sample")}",
            $"GPU clock: {(clockMHz is double clock ? clock.ToString("0.##", CultureInfo.InvariantCulture) + " MHz" : "not measured")}",
            $"GPU utilization: {(utilizationPercent is double util ? util.ToString("0.##", CultureInfo.InvariantCulture) + "%" : "not in this sample")}",
            $"Thermal: {thermal ?? "not in this sample"}"
        ]);
    }
}
