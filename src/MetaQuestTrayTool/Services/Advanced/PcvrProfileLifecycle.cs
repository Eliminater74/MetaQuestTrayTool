using System.IO;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed record PcvrProfileContext
{
    public bool SessionActive { get; init; }
    public string? ActiveProvider { get; init; }
    public string? TrustedSerial { get; init; }
    public string? HeadsetSerial { get; init; }
    public string? AdbSerial { get; init; }
    public string? Model { get; init; }
    public string? Fingerprint { get; init; }
    public bool HeadsetReady { get; init; }
    public bool ExperimentalGpuOptIn { get; init; }
    public double? BatteryCelsius { get; init; }
    public bool ThermalReapplyBlocked { get; init; }
}

public interface IPcvrProfileActions
{
    bool OwnsRestore { get; }
    string ApplyExperimentalGpu(PcvrProfileContext context);
    string RestoreExperimentalGpu(string serial);
}

/// <summary>
/// Applies a game profile's supported PCVR preferences and restores them on exit.
/// It does not launch a provider, edit an ALVR or Pyrowave session, or write an experimental refresh.
/// </summary>
public sealed class PcvrProfileLifecycle
{
    private readonly object _gate = new();
    private int _generation;
    private AppliedGpu? _applied;
    internal Action<int>? BeforeCommit { get; set; }

    public ProfileStepResult Apply(PcvrProfilePreferences preferences, PcvrProfileContext context, IPcvrProfileActions actions)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var generation = Interlocked.Increment(ref _generation);
        var plan = BuildPlan(preferences, context);
        BeforeCommit?.Invoke(generation);
        lock (_gate)
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return new ProfileStepResult(
                    "PCVR",
                    ProfileStepStatus.Skipped,
                    "A newer profile operation started. This one wrote nothing and did not launch a provider.");
            }

            if (!plan.WriteGpu)
            {
                return new ProfileStepResult("PCVR", ProfileStepStatus.Skipped, plan.Summary);
            }

            try
            {
                var written = actions.ApplyExperimentalGpu(context);
                if (actions.OwnsRestore)
                {
                    _applied = new AppliedGpu(generation, context.AdbSerial ?? string.Empty);
                }

                return new ProfileStepResult("PCVR", ProfileStepStatus.Succeeded, written + " " + plan.Summary);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException)
            {
                return new ProfileStepResult("PCVR", ProfileStepStatus.Failed, ex.Message + " No streaming provider was launched.");
            }
        }
    }

    public ProfileStepResult Restore(IPcvrProfileActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var generation = Volatile.Read(ref _generation);
        lock (_gate)
        {
            if (generation != _generation)
            {
                return new ProfileStepResult("PCVR", ProfileStepStatus.Skipped, "A newer profile operation owns the headset settings. This restore wrote nothing.");
            }

            if (_applied is null)
            {
                return new ProfileStepResult("PCVR", ProfileStepStatus.Skipped, "No PCVR profile setting was applied, so nothing was restored. No streaming provider was launched.");
            }

            try
            {
                var summary = actions.RestoreExperimentalGpu(_applied.Serial);
                _applied = null;
                return new ProfileStepResult("PCVR", ProfileStepStatus.Succeeded, summary);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException)
            {
                return new ProfileStepResult("PCVR", ProfileStepStatus.Failed, "Profile GPU restore failed: " + ex.Message);
            }
        }
    }

    private ProfilePlan BuildPlan(PcvrProfilePreferences preferences, PcvrProfileContext context)
    {
        var notes = new List<string>();
        if (preferences.AutoSwitchProvider)
        {
            notes.Add("Auto-switch is stored but was ignored. No streaming provider was launched or stopped.");
        }

        if (!ProvidersMatch(preferences.PreferredProvider, context.ActiveProvider, out var providerReason))
        {
            notes.Add(providerReason);
            notes.AddRange(RecordedOnly(preferences));
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (!context.SessionActive)
        {
            notes.Add("The PCVR session is not active. No headset property was written.");
            notes.AddRange(RecordedOnly(preferences));
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        notes.AddRange(RecordedOnly(preferences));
        if (preferences.ExperimentalGpuMaximum != true)
        {
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (context.ThermalReapplyBlocked)
        {
            notes.Add("Automatic reapplication of GPU level 7 is blocked after a thermal restoration. This profile did not write it.");
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (!context.ExperimentalGpuOptIn)
        {
            notes.Add("GPU level 7 stays under the Headset page opt-in. This profile did not write it.");
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (!context.HeadsetReady
            || string.IsNullOrWhiteSpace(context.HeadsetSerial)
            || string.IsNullOrWhiteSpace(context.AdbSerial)
            || string.IsNullOrWhiteSpace(context.TrustedSerial)
            || !string.Equals(context.HeadsetSerial, context.TrustedSerial, StringComparison.Ordinal))
        {
            notes.Add("Headset identity was not confirmed for the trusted headset. GPU level 7 was not written.");
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (string.IsNullOrWhiteSpace(context.Fingerprint))
        {
            notes.Add("Firmware fingerprint was not read. GPU level 7 was not written.");
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        var decision = ExperimentalGpuPolicy.Evaluate(true, context.Model, context.BatteryCelsius);
        if (!decision.ApplyLevel7)
        {
            notes.Add(decision.Refusal ?? "Experimental GPU level 7 was not written.");
            return new ProfilePlan(false, string.Join(" ", notes));
        }

        if (!string.IsNullOrWhiteSpace(decision.Warning))
        {
            notes.Add(decision.Warning);
        }

        notes.Add(ExperimentalGpuThermalPolicy.GateDisclaimer);
        notes.Add("No streaming provider was launched and no session file was edited.");
        return new ProfilePlan(true, string.Join(" ", notes));
    }

    private static IEnumerable<string> RecordedOnly(PcvrProfilePreferences preferences)
    {
        if (preferences.ExperimentalRefreshHz is not null)
        {
            yield return "Experimental refresh " + preferences.ExperimentalRefreshHz.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " Hz was not written. A game profile does not change the panel rate, because that can interrupt the session.";
        }

        if (!string.IsNullOrWhiteSpace(preferences.AlvrProfile) || !string.IsNullOrWhiteSpace(preferences.PyrowaveProfile))
        {
            yield return "Named ALVR or Pyrowave presets were not applied. No session file was edited and no dashboard request was sent.";
        }
    }

    public static bool OwnsNewGpuOverride(string? currentValue) =>
        !string.Equals(currentValue?.Trim(), ExperimentalGpuPolicy.MaximumLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    internal static bool ProvidersMatch(string? preferred, string? active, out string reason)
    {
        if (string.IsNullOrWhiteSpace(preferred))
        {
            reason = "No provider was named on the profile, so provider ownership was not confirmed. No headset property was written.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(active))
        {
            reason = "The active streaming provider was not confirmed. Preferred provider '" + preferred.Trim() + "' was not started and no headset property was written.";
            return false;
        }

        if (!string.Equals(Normalize(preferred), Normalize(active), StringComparison.OrdinalIgnoreCase))
        {
            reason = "Active provider '" + active.Trim() + "' does not match preferred provider '" + preferred.Trim() + "'. No provider was switched and no headset property was written.";
            return false;
        }

        reason = "Provider '" + active.Trim() + "' matches the profile.";
        return true;
    }

    private static string Normalize(string value)
    {
        var text = value.Trim();
        if (text.Equals("Link", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Meta Link", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Air Link", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Quest Link", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Meta Quest Link", StringComparison.OrdinalIgnoreCase))
        {
            return "meta quest link";
        }

        if (text.Equals("Virtual Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return "virtual desktop";
        }

        return text;
    }

    private sealed record AppliedGpu(int Generation, string Serial);

    private sealed record ProfilePlan(bool WriteGpu, string Summary);
}
