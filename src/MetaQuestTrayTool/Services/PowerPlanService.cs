using System.Diagnostics;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class PowerPlanService
{
    public sealed record UsbSelectiveSuspendState(int? AcValue, int? DcValue);

    private static readonly Regex PlanLine = new(
        @"\s*([0-9a-fA-F-]{36})\s+\((.+?)\)(\s+\*)?\s*$",
        RegexOptions.Compiled);

    public IReadOnlyList<PowerPlanInfo> ListPlans()
    {
        var output = RunPowerCfg("/list");
        var plans = new List<PowerPlanInfo>();

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PlanLine.Match(line);
            if (!match.Success || !Guid.TryParse(match.Groups[1].Value, out var guid))
            {
                continue;
            }

            plans.Add(new PowerPlanInfo
            {
                Guid = guid,
                Name = match.Groups[2].Value.Trim(),
                IsActive = match.Groups[3].Success
            });
        }

        return plans;
    }

    public PowerPlanInfo? GetActivePlan() => ListPlans().FirstOrDefault(plan => plan.IsActive);

    public string SetActivePlan(Guid planGuid)
    {
        try
        {
            RunPowerCfg($"/setactive {planGuid}");
            var name = ListPlans().FirstOrDefault(plan => plan.Guid == planGuid)?.Name ?? planGuid.ToString();
            return $"Power plan set to '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Could not change power plan: {ex.Message}";
        }
    }

    public string SetActivePlan(string? planGuidText)
    {
        if (!Guid.TryParse(planGuidText, out var guid))
        {
            return "No power plan GUID is configured.";
        }

        return SetActivePlan(guid);
    }

    public string CaptureCurrentAsFallback(PowerSettings settings)
    {
        var active = GetActivePlan();
        settings.FallbackPlanGuid = active?.Guid.ToString("D");
        return active is null
            ? "Could not read the active power plan."
            : $"Stored fallback power plan '{active.Name}'.";
    }

    public string ApplyVrPlan(PowerSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FallbackPlanGuid))
        {
            CaptureCurrentAsFallback(settings);
        }

        return SetActivePlan(settings.VrPlanGuid);
    }

    public string RestoreFallbackPlan(PowerSettings settings) => SetActivePlan(settings.FallbackPlanGuid);

    public string SetUsbSelectiveSuspend(bool enabled)
    {
        return SetUsbSelectiveSuspend(new UsbSelectiveSuspendState(
            enabled ? 1 : 0,
            enabled ? 1 : 0), restoring: false);
    }

    public UsbSelectiveSuspendState? CaptureUsbSelectiveSuspend()
    {
        try
        {
            var output = RunPowerCfg(
                "/query SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 " +
                "48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
            var ac = ParsePowerIndex(output, "Current AC Power Setting Index");
            var dc = ParsePowerIndex(output, "Current DC Power Setting Index");
            return ac is null && dc is null ? null : new UsbSelectiveSuspendState(ac, dc);
        }
        catch
        {
            return null;
        }
    }

    public string RestoreUsbSelectiveSuspend(UsbSelectiveSuspendState? state)
    {
        if (state is null)
        {
            return "USB selective suspend baseline was not captured.";
        }

        return SetUsbSelectiveSuspend(state, restoring: true);
    }

    private string SetUsbSelectiveSuspend(UsbSelectiveSuspendState state, bool restoring)
    {
        try
        {
            if (state.AcValue is int ac)
            {
                RunPowerCfg($"/SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 {ac}");
            }

            if (state.DcValue is int dc)
            {
                RunPowerCfg($"/SETDCVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 {dc}");
            }

            RunPowerCfg("/SETACTIVE SCHEME_CURRENT");
            return restoring
                ? $"USB selective suspend restored (AC={state.AcValue?.ToString() ?? "unknown"}, DC={state.DcValue?.ToString() ?? "unknown"})."
                : state.AcValue == 1
                    ? "USB selective suspend enabled for the active plan."
                    : "USB selective suspend disabled for the active plan.";
        }
        catch (Exception ex)
        {
            return $"Could not change USB selective suspend: {ex.Message}";
        }
    }

    private static int? ParsePowerIndex(string output, string label)
    {
        var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.Contains(label, StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return null;
        }

        var match = Regex.Match(line, @"0x([0-9a-fA-F]+)");
        return match.Success && int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var value)
            ? value
            : null;
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        }

        return output;
    }
}
