namespace MetaQuestTrayTool.Models;

/// <summary>Live headset telemetry via ADB (battery / charge / Wi‑Fi). Optional — needs debugging.</summary>
public sealed class HeadsetRuntimeStatus
{
    public int? BatteryPercent { get; init; }
    public bool? IsCharging { get; init; }
    public string? ChargeStatus { get; init; }
    public string? WifiSsid { get; init; }
    public int? WifiRssi { get; init; }
    public bool Available { get; init; }
    public string? Error { get; init; }

    public string Summary
    {
        get
        {
            if (!Available)
            {
                return Error ?? "unavailable (need ADB)";
            }

            var parts = new List<string>();
            if (BatteryPercent is int pct)
            {
                var charge = IsCharging == true
                    ? "charging"
                    : IsCharging == false
                        ? "on battery"
                        : ChargeStatus?.ToLowerInvariant() ?? "";
                parts.Add(string.IsNullOrWhiteSpace(charge) ? $"{pct}%" : $"{pct}% ({charge})");
            }

            if (!string.IsNullOrWhiteSpace(WifiSsid))
            {
                var rssi = WifiRssi is int r ? $" {r} dBm" : "";
                parts.Add($"Wi‑Fi “{WifiSsid}”{rssi}");
            }
            else if (WifiRssi is int r)
            {
                parts.Add($"Wi‑Fi {r} dBm");
            }

            return parts.Count == 0 ? "connected (no battery/Wi‑Fi details)" : string.Join(" · ", parts);
        }
    }
}
