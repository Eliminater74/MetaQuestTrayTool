using System.Diagnostics;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class PowerPlanService
{
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
        // AC and DC indexes for USB selective suspend under the active scheme.
        var value = enabled ? "1" : "0";
        try
        {
            RunPowerCfg($"/SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 {value}");
            RunPowerCfg($"/SETDCVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 {value}");
            RunPowerCfg("/SETACTIVE SCHEME_CURRENT");
            return enabled
                ? "USB selective suspend enabled for the active plan."
                : "USB selective suspend disabled for the active plan.";
        }
        catch (Exception ex)
        {
            return $"Could not change USB selective suspend: {ex.Message}";
        }
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
