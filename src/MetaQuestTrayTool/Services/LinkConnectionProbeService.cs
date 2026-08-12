using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Best-effort PCVR transport detection.
/// Meta exposes <c>isUsingAirLink</c> in <c>%LocalAppData%\Oculus\DeviceCache.json</c>
/// (confirmed against Oculus client device cache; not in RemoteHeadset registry).
/// Steam Link / Virtual Desktop are inferred from PC processes.
/// </summary>
public sealed class LinkConnectionProbeService
{
    private static readonly string DeviceCachePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oculus", "DeviceCache.json");

    // Oculus / Meta USB vendor IDs commonly used by Quest / Rift hardware.
    private static readonly string[] OculusUsbVidPrefixes =
    [
        "VID_2833",
        "VID_2BEC"
    ];

    private readonly App _app;

    public LinkConnectionProbeService(App app)
    {
        _app = app;
    }

    public VrConnectionStatus Probe()
    {
        var steamVr = IsProcessRunning("vrserver") || IsProcessRunning("vrmonitor");
        var virtualDesktop = IsProcessRunning("VirtualDesktop.Streamer")
                             || IsProcessRunning("VirtualDesktop.Server")
                             || IsProcessRunning("VirtualDesktop.Service");
        var usb = IsOculusUsbPresent();
        var metaHmd = MetaHmdReported();
        var cache = ReadHeadsetCache();

        var cacheConnected = cache is not null && IsConnectedState(cache.ConnectionState, cache.RdConnectionState);
        var metaSession = cacheConnected || metaHmd;

        if (virtualDesktop && !metaSession)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.VirtualDesktop,
                Summary = "Virtual Desktop (wireless)",
                Detail = "Streamer/server process detected",
                SessionActive = true,
                UsbHeadsetPresent = usb,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = true,
                IsUsingAirLink = cache?.IsUsingAirLink,
                HeadsetSerial = cache?.SerialNumber,
                DeviceCacheConnectionState = cache?.ConnectionState
            };
        }

        if (steamVr && !metaSession)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.SteamLinkOrSteamVr,
                Summary = "SteamVR session (Steam Link / SteamVR)",
                Detail = "vrserver/vrmonitor running without an active Meta Link HMD",
                SessionActive = true,
                UsbHeadsetPresent = usb,
                MetaHmdReported = metaHmd,
                SteamVrRunning = true,
                VirtualDesktopRunning = virtualDesktop,
                IsUsingAirLink = cache?.IsUsingAirLink,
                HeadsetSerial = cache?.SerialNumber,
                DeviceCacheConnectionState = cache?.ConnectionState
            };
        }

        if (metaSession)
        {
            return ClassifyMetaSession(cache, usb, metaHmd, steamVr, virtualDesktop, sessionActive: true);
        }

        if (cache?.IsUsingAirLink is bool lastAir)
        {
            return new VrConnectionStatus
            {
                Kind = lastAir ? VrConnectionKind.MetaAirLink : VrConnectionKind.MetaWiredLink,
                Summary = lastAir
                    ? "Meta Air Link (last known)"
                    : "Meta wired Link (last known)",
                Detail = $"DeviceCache isUsingAirLink={lastAir}; no active Meta HMD session",
                SessionActive = false,
                IsUsingAirLink = lastAir,
                HeadsetSerial = cache.SerialNumber,
                DeviceCacheConnectionState = cache.ConnectionState,
                UsbHeadsetPresent = usb,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = virtualDesktop
            };
        }

        if (usb)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.Unknown,
                Summary = "Oculus/Meta USB device present (no Link session)",
                Detail = "Headset may be charging or in ADB-only mode",
                SessionActive = false,
                UsbHeadsetPresent = true,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = virtualDesktop,
                HeadsetSerial = cache?.SerialNumber,
                DeviceCacheConnectionState = cache?.ConnectionState
            };
        }

        return new VrConnectionStatus
        {
            Kind = VrConnectionKind.Idle,
            Summary = "No PCVR Link session detected",
            Detail = File.Exists(DeviceCachePath)
                ? "Meta DeviceCache present but idle"
                : "Meta DeviceCache.json not found",
            SessionActive = false,
            UsbHeadsetPresent = false,
            MetaHmdReported = metaHmd,
            SteamVrRunning = steamVr,
            VirtualDesktopRunning = virtualDesktop,
            HeadsetSerial = cache?.SerialNumber,
            DeviceCacheConnectionState = cache?.ConnectionState,
            IsUsingAirLink = cache?.IsUsingAirLink
        };
    }

    private VrConnectionStatus ClassifyMetaSession(
        HeadsetCacheEntry? cache,
        bool usb,
        bool metaHmd,
        bool steamVr,
        bool virtualDesktop,
        bool sessionActive)
    {
        // Meta's own flag — primary signal for Air vs wired.
        if (cache?.IsUsingAirLink == true)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.MetaAirLink,
                Summary = "Meta Air Link",
                Detail = BuildMetaDetail(cache, usb, "DeviceCache isUsingAirLink=true"),
                SessionActive = sessionActive,
                IsUsingAirLink = true,
                HeadsetSerial = cache.SerialNumber,
                DeviceCacheConnectionState = cache.ConnectionState,
                UsbHeadsetPresent = usb,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = virtualDesktop
            };
        }

        if (cache?.IsUsingAirLink == false)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.MetaWiredLink,
                Summary = "Meta wired Link",
                Detail = BuildMetaDetail(cache, usb, "DeviceCache isUsingAirLink=false"),
                SessionActive = sessionActive,
                IsUsingAirLink = false,
                HeadsetSerial = cache.SerialNumber,
                DeviceCacheConnectionState = cache.ConnectionState,
                UsbHeadsetPresent = usb,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = virtualDesktop
            };
        }

        // Cache missing the flag — fall back to USB presence.
        if (usb)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.MetaWiredLink,
                Summary = "Meta wired Link (USB heuristic)",
                Detail = "Meta HMD session + Oculus USB device; DeviceCache isUsingAirLink unavailable",
                SessionActive = sessionActive,
                UsbHeadsetPresent = true,
                MetaHmdReported = metaHmd,
                SteamVrRunning = steamVr,
                VirtualDesktopRunning = virtualDesktop,
                HeadsetSerial = cache?.SerialNumber,
                DeviceCacheConnectionState = cache?.ConnectionState
            };
        }

        return new VrConnectionStatus
        {
            Kind = VrConnectionKind.MetaLinkUnknownTransport,
            Summary = "Meta Link (transport unknown)",
            Detail = "Meta HMD session active but DeviceCache isUsingAirLink unavailable and no Oculus USB VID",
            SessionActive = sessionActive,
            UsbHeadsetPresent = false,
            MetaHmdReported = metaHmd,
            SteamVrRunning = steamVr,
            VirtualDesktopRunning = virtualDesktop,
            HeadsetSerial = cache?.SerialNumber,
            DeviceCacheConnectionState = cache?.ConnectionState
        };
    }

    private static string BuildMetaDetail(HeadsetCacheEntry cache, bool usb, string primary)
    {
        var parts = new List<string>
        {
            primary,
            $"connectionState={cache.ConnectionState ?? "—"}",
            $"rdConnectionState={cache.RdConnectionState ?? "—"}",
            usb ? "USB VID present" : "no Oculus USB VID"
        };
        if (!string.IsNullOrWhiteSpace(cache.SerialNumber))
        {
            parts.Add($"serial={cache.SerialNumber}");
        }

        return string.Join("; ", parts);
    }

    private bool MetaHmdReported()
    {
        try
        {
            if (!_app.DebugTool.IsAvailable)
            {
                return false;
            }

            return _app.DebugTool.EnumerateHeadsets().Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessRunning(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsConnectedState(string? connectionState, string? rdConnectionState)
    {
        return LooksConnected(connectionState) || LooksConnected(rdConnectionState);
    }

    private static bool LooksConnected(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        return state.Contains("connect", StringComparison.OrdinalIgnoreCase)
               && !state.Contains("disconnect", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOculusUsbPresent()
    {
        try
        {
            using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
            if (usb is null)
            {
                return false;
            }

            foreach (var vid in usb.GetSubKeyNames())
            {
                if (!OculusUsbVidPrefixes.Any(prefix =>
                        vid.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                using var vidKey = usb.OpenSubKey(vid);
                if (vidKey is null)
                {
                    continue;
                }

                // Presence of an instance key usually means Windows enumerated the device at least once;
                // prefer instances that still look plugged in via the Device Parameters / Container.
                foreach (var instance in vidKey.GetSubKeyNames())
                {
                    using var inst = vidKey.OpenSubKey(instance);
                    if (inst is null)
                    {
                        continue;
                    }

                    // ConfigFlags bit 1 (CONFIGFLAG_REMOVED) often marks yanked devices.
                    var configFlags = inst.GetValue("ConfigFlags") as int? ?? 0;
                    if ((configFlags & 0x2) != 0)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }
        catch
        {
            // Registry may be restricted on some systems.
        }

        return false;
    }

    private static HeadsetCacheEntry? ReadHeadsetCache()
    {
        try
        {
            if (!File.Exists(DeviceCachePath))
            {
                return null;
            }

            using var stream = File.OpenRead(DeviceCachePath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("devices", out var devices)
                || devices.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            HeadsetCacheEntry? best = null;
            foreach (var device in devices.EnumerateArray())
            {
                if (!device.TryGetProperty("type", out var typeEl)
                    || !string.Equals(typeEl.GetString(), "headset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entry = new HeadsetCacheEntry
                {
                    SerialNumber = GetString(device, "serialNumber") ?? GetString(device, "id"),
                    ConnectionState = GetString(device, "connectionState"),
                    RdConnectionState = GetString(device, "rdConnectionState"),
                    IsUsingAirLink = GetBool(device, "isUsingAirLink"),
                    LastSeenAt = GetInt64(device, "lastSeenAt") ?? 0,
                    SupportsOculusLink = GetBool(device, "supportsOculusLink")
                };

                if (best is null
                    || entry.LastSeenAt > best.LastSeenAt
                    || (IsConnectedState(entry.ConnectionState, entry.RdConnectionState)
                        && !IsConnectedState(best.ConnectionState, best.RdConnectionState)))
                {
                    best = entry;
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static long? GetInt64(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
        {
            return n;
        }

        return null;
    }

    private sealed class HeadsetCacheEntry
    {
        public string? SerialNumber { get; init; }
        public string? ConnectionState { get; init; }
        public string? RdConnectionState { get; init; }
        public bool? IsUsingAirLink { get; init; }
        public long LastSeenAt { get; init; }
        public bool? SupportsOculusLink { get; init; }
    }
}
