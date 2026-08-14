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
    private readonly object _cacheLock = new();
    private VrConnectionStatus? _cachedProbe;
    private bool _cachedIncludeEnumHmd;
    private bool _cachedIncludeAudioLink;
    private long _cachedProbeTicks;
    private static readonly long ProbeCacheTicks = TimeSpan.FromSeconds(5).Ticks;

    private bool? _cachedUsbPresent;
    private long _cachedUsbTicks;
    private static readonly long UsbCacheTicks = TimeSpan.FromSeconds(5).Ticks;

    private readonly Dictionary<string, (bool Running, long Ticks)> _processCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly long ProcessCacheTicks = TimeSpan.FromSeconds(5).Ticks;

    public LinkConnectionProbeService(App app)
    {
        _app = app;
    }

    public VrConnectionStatus Probe(bool includeEnumHmd = true, bool includeAudioLink = true)
    {
        var now = DateTime.UtcNow.Ticks;
        lock (_cacheLock)
        {
            if (_cachedProbe is not null
                && _cachedIncludeEnumHmd == includeEnumHmd
                && _cachedIncludeAudioLink == includeAudioLink
                && now - _cachedProbeTicks < ProbeCacheTicks)
            {
                return _cachedProbe;
            }
        }

        var status = ProbeCore(includeEnumHmd, includeAudioLink);
        lock (_cacheLock)
        {
            _cachedProbe = status;
            _cachedIncludeEnumHmd = includeEnumHmd;
            _cachedIncludeAudioLink = includeAudioLink;
            _cachedProbeTicks = DateTime.UtcNow.Ticks;
        }

        return status;
    }

    private VrConnectionStatus ProbeCore(bool includeEnumHmd, bool includeAudioLink)
    {
        // Quest Steam Link runs full SteamVR (vrserver). vrmonitor alone can be a leftover UI.
        var steamVr = IsProcessRunning("vrserver")
                      || (IsProcessRunning("vrmonitor") && IsProcessRunning("vrcompositor"));
        var virtualDesktop = IsProcessRunning("VirtualDesktop.Streamer")
                             || IsProcessRunning("VirtualDesktop.Server")
                             || IsProcessRunning("VirtualDesktop.Service");
        var usb = IsOculusUsbPresent();
        var metaHmd = includeEnumHmd && MetaHmdReported();
        var cache = ReadHeadsetCache();
        var audioLink = false;
        if (includeAudioLink)
        {
            try
            {
                audioLink = _app.Audio.IsLinkAudioSessionActive(_app.Settings.Current.Audio);
            }
            catch
            {
                // audio probe optional
            }
        }

        // Healthy Meta Link wins only with a strong live signal. Meta often auto-connects when
        // the headset wakes on Wi‑Fi (DeviceCache connected/primary) without launching Link —
        // that must not hide an active Steam Link / SteamVR or Virtual Desktop session.
        // EnumHmd alone is also weak while SteamVR is up (auto-connect ghosts during Steam Link).
        var strongMeta = LooksLikeStrongMetaSession(cache, metaHmd, audioLink, steamVr);
        var cacheMeta = LooksLikeActiveMetaSession(cache, metaHmd, audioLink, steamVr);

        if (strongMeta)
        {
            return ClassifyMetaSession(
                cache, usb, metaHmd, steamVr, virtualDesktop, sessionActive: true, metaLinkStreaming: true);
        }

        if (virtualDesktop)
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

        if (steamVr)
        {
            var staleMeta = cache is not null
                            && (string.Equals(cache.OperationalState, "inoperable", StringComparison.OrdinalIgnoreCase)
                                || IsConnectedState(cache.ConnectionState, cache.RdConnectionState)
                                || string.Equals(cache.PrimaryState, "primary", StringComparison.OrdinalIgnoreCase));
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.SteamLinkOrSteamVr,
                Summary = "SteamVR session (Steam Link / SteamVR)",
                Detail = staleMeta
                    ? "SteamVR running — Meta DeviceCache may still show auto-connect/inoperable (normal; Steam Link is active)"
                    : "vrserver running without an active Meta Link session",
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

        // No Steam/VD — DeviceCache-only Meta (including auto-connect / broken) is fine to show.
        if (cacheMeta)
        {
            var streaming = LooksLikeStreamingMetaLink(cache, metaHmd, audioLink, steamVr);
            return ClassifyMetaSession(
                cache, usb, metaHmd, steamVr, virtualDesktop, sessionActive: true, metaLinkStreaming: streaming);
        }

        var brokenMeta = TryDescribeBrokenMetaSession(cache, usb, metaHmd, steamVr, virtualDesktop);
        if (brokenMeta is not null)
        {
            return brokenMeta;
        }

        // Meta leaves isUsingAirLink sticky after you quit Link / power off the headset.
        // Never present that as a live session — Kind=Idle with an optional "last used" note.
        if (cache?.IsUsingAirLink is bool lastAir)
        {
            var last = lastAir ? "Air Link" : "wired Link";
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.Idle,
                Summary = "Not connected",
                Detail =
                    $"No active Meta / Steam / VD session. DeviceCache still remembers last transport as {last}"
                    + (string.IsNullOrWhiteSpace(cache.SerialNumber) ? "" : $" ({cache.SerialNumber})")
                    + $"; connectionState={cache.ConnectionState ?? "—"}.",
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

    public VrSessionCapabilities GetCapabilities() => VrSessionCapabilities.From(Probe());

    /// <summary>
    /// Live Meta Link stream signals that should beat SteamVR/VD process detection
    /// (e.g. Meta Link + SteamVR OpenXR). DeviceCache auto-connect and EnumHmd alone are not
    /// enough while SteamVR is running — Meta often reports the headset when it wakes on Wi‑Fi
    /// without launching Link, which would otherwise unblock SS/Link applies under Steam Link.
    /// </summary>
    private static bool LooksLikeStrongMetaSession(
        HeadsetCacheEntry? cache,
        bool metaHmd,
        bool audioLink,
        bool steamVrRunning)
    {
        if (audioLink)
        {
            return true;
        }

        if (cache is not null
            && !string.Equals(cache.OperationalState, "inoperable", StringComparison.OrdinalIgnoreCase)
            && LooksConnected(cache.RdConnectionState))
        {
            return true;
        }

        // EnumHmd without SteamVR: treat as Meta. With SteamVR: ignore — auto-connect ghosts.
        if (metaHmd && !steamVrRunning)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Stricter than <see cref="LooksLikeActiveMetaSession"/> — excludes operable/primary DeviceCache
    /// ghosts when the headset is on Wi‑Fi but Link is not streaming.
    /// </summary>
    private static bool LooksLikeStreamingMetaLink(
        HeadsetCacheEntry? cache,
        bool metaHmd,
        bool audioLink,
        bool steamVrRunning)
    {
        if (LooksLikeStrongMetaSession(cache, metaHmd, audioLink, steamVrRunning))
        {
            return true;
        }

        if (cache is null)
        {
            return false;
        }

        if (string.Equals(cache.OperationalState, "inoperable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsConnectedState(cache.ConnectionState, cache.RdConnectionState);
    }

    private static bool LooksLikeActiveMetaSession(
        HeadsetCacheEntry? cache,
        bool metaHmd,
        bool audioLink,
        bool steamVrRunning = false)
    {
        if (LooksLikeStrongMetaSession(cache, metaHmd, audioLink, steamVrRunning))
        {
            return true;
        }

        if (cache is null)
        {
            return false;
        }

        // "connected" + inoperable is a failed / half Link session — do not treat as healthy.
        if (string.Equals(cache.OperationalState, "inoperable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsConnectedState(cache.ConnectionState, cache.RdConnectionState))
        {
            return true;
        }

        // Air Link often keeps rdConnectionState=disconnected while connectionState lags;
        // Meta still marks the headset primary/operable/active while streaming.
        var operable = string.Equals(cache.OperationalState, "operable", StringComparison.OrdinalIgnoreCase);
        var powered = string.Equals(cache.PowerState, "active", StringComparison.OrdinalIgnoreCase);
        var primary = string.Equals(cache.PrimaryState, "primary", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(cache.PrimaryState, "alternate", StringComparison.OrdinalIgnoreCase);
        if (operable && powered && primary)
        {
            return true;
        }

        return false;
    }

    private VrConnectionStatus? TryDescribeBrokenMetaSession(
        HeadsetCacheEntry? cache,
        bool usb,
        bool metaHmd,
        bool steamVr,
        bool virtualDesktop)
    {
        if (cache is null)
        {
            return null;
        }

        var inoperable = string.Equals(cache.OperationalState, "inoperable", StringComparison.OrdinalIgnoreCase);
        var connectedish = IsConnectedState(cache.ConnectionState, cache.RdConnectionState)
                           || string.Equals(cache.PrimaryState, "primary", StringComparison.OrdinalIgnoreCase);
        if (!inoperable || !connectedish)
        {
            return null;
        }

        var air = cache.IsUsingAirLink == true;
        return new VrConnectionStatus
        {
            Kind = air ? VrConnectionKind.MetaAirLink : VrConnectionKind.MetaWiredLink,
            Summary = air
                ? "Meta Air Link — auto-connect / not streaming"
                : "Meta wired Link — auto-connect / not streaming",
            Detail =
                $"DeviceCache connectionState={cache.ConnectionState}; operationalState=inoperable; "
                + $"rdConnectionState={cache.RdConnectionState}. "
                + "Normal when the headset is on Wi‑Fi without opening Link (or after a failed Link init).",
            SessionActive = false,
            IsUsingAirLink = cache.IsUsingAirLink,
            HeadsetSerial = cache.SerialNumber,
            DeviceCacheConnectionState = cache.ConnectionState,
            UsbHeadsetPresent = usb,
            MetaHmdReported = metaHmd,
            SteamVrRunning = steamVr,
            VirtualDesktopRunning = virtualDesktop
        };
    }

    private VrConnectionStatus ClassifyMetaSession(
        HeadsetCacheEntry? cache,
        bool usb,
        bool metaHmd,
        bool steamVr,
        bool virtualDesktop,
        bool sessionActive,
        bool metaLinkStreaming)
    {
        // Meta's own flag — primary signal for Air vs wired.
        if (cache?.IsUsingAirLink == true)
        {
            return new VrConnectionStatus
            {
                Kind = VrConnectionKind.MetaAirLink,
                Summary = "Meta Air Link",
                Detail = BuildMetaDetail(cache, usb, "wireless Air Link (isUsingAirLink=true)"),
                SessionActive = sessionActive,
                MetaLinkStreaming = metaLinkStreaming,
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
                Detail = BuildMetaDetail(cache, usb, "wired Link (isUsingAirLink=false)"),
                SessionActive = sessionActive,
                MetaLinkStreaming = metaLinkStreaming,
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
                MetaLinkStreaming = metaLinkStreaming,
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
            MetaLinkStreaming = metaLinkStreaming,
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
            $"primary={cache.PrimaryState ?? "—"}",
            $"power={cache.PowerState ?? "—"}",
            usb
                ? "Oculus USB VID present (often charge/ADB cable — does not mean wired Link)"
                : "no Oculus USB VID"
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

    private bool IsProcessRunning(string name)
    {
        var now = DateTime.UtcNow.Ticks;
        lock (_cacheLock)
        {
            if (_processCache.TryGetValue(name, out var cached) && now - cached.Ticks < ProcessCacheTicks)
            {
                return cached.Running;
            }
        }

        bool running;
        try
        {
            running = Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            running = false;
        }

        lock (_cacheLock)
        {
            _processCache[name] = (running, DateTime.UtcNow.Ticks);
        }

        return running;
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

    private bool IsOculusUsbPresent()
    {
        var now = DateTime.UtcNow.Ticks;
        lock (_cacheLock)
        {
            if (_cachedUsbPresent is not null && now - _cachedUsbTicks < UsbCacheTicks)
            {
                return _cachedUsbPresent.Value;
            }
        }

        var present = IsOculusUsbPresentCore();
        lock (_cacheLock)
        {
            _cachedUsbPresent = present;
            _cachedUsbTicks = DateTime.UtcNow.Ticks;
        }

        return present;
    }

    private static bool IsOculusUsbPresentCore()
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

                foreach (var instance in vidKey.GetSubKeyNames())
                {
                    using var inst = vidKey.OpenSubKey(instance);
                    if (inst is null)
                    {
                        continue;
                    }

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

            // Share with the Meta client — exclusive OpenRead can fail while DeviceCache is updating.
            using var stream = new FileStream(
                DeviceCachePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
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
                    SupportsOculusLink = GetBool(device, "supportsOculusLink"),
                    PowerState = GetString(device, "powerState"),
                    OperationalState = GetString(device, "operationalState"),
                    PrimaryState = GetString(device, "primaryState")
                };

                if (best is null
                    || entry.LastSeenAt > best.LastSeenAt
                    || (LooksLikeActiveMetaSession(entry, metaHmd: false, audioLink: false)
                        && !LooksLikeActiveMetaSession(best, metaHmd: false, audioLink: false)))
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
        public string? PowerState { get; init; }
        public string? OperationalState { get; init; }
        public string? PrimaryState { get; init; }
    }
}
