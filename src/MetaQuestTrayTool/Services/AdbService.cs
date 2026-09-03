using System.Diagnostics;
using System.IO;
using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class AdbDevice
{
    public required string Serial { get; init; }
    public required string State { get; init; }
    public string? Model { get; init; }
    public string? Product { get; init; }
    public string? DeviceName { get; init; }

    public bool IsReady => State.Equals("device", StringComparison.OrdinalIgnoreCase);
    public bool NeedsAuthorization => State.Equals("unauthorized", StringComparison.OrdinalIgnoreCase);
}

public sealed class AdbService
{
    public string? AdbPath { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(AdbPath) && File.Exists(AdbPath);

    private readonly object _cacheLock = new();
    private IReadOnlyList<AdbDevice>? _cachedDevices;
    private DateTime _cachedDevicesUtc = DateTime.MinValue;
    private static readonly TimeSpan DevicesCache = TimeSpan.FromSeconds(5);
    private HeadsetRuntimeStatus? _cachedRuntime;
    private string? _cachedRuntimeSerial;
    private DateTime _cachedRuntimeUtc = DateTime.MinValue;
    private static readonly TimeSpan RuntimeCache = TimeSpan.FromSeconds(20);
    private readonly SemaphoreSlim _commandGate = new(1, 1);

    public void Refresh()
    {
        if (!string.IsNullOrWhiteSpace(AdbPath) && File.Exists(AdbPath))
        {
            return;
        }

        AdbPath = FindAdb();
    }

    /// <summary>
    /// Stops this app's bundled ADB process and only terminates packaged ADB processes so Setup
    /// can replace platform-tools without disrupting Android Studio, SideQuest, or other ADB clients.
    /// </summary>
    public string KillServerForUpdate()
    {
        Refresh();
        if (!_commandGate.Wait(TimeSpan.FromSeconds(30)))
        {
            return "ADB command queue was busy for 30s; could not kill bundled adb.exe for update yet.";
        }

        var parts = new List<string>();

        try
        {
            var killed = 0;
            foreach (var process in Process.GetProcessesByName("adb"))
            {
                try
                {
                    using (process)
                    {
                        if (!IsBundledProcess(process))
                        {
                            continue;
                        }

                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                        killed++;
                    }
                }
                catch
                {
                    // Access denied / already exiting
                }
            }

            parts.Add(killed == 0
                ? "No bundled adb.exe processes left (shared ADB server was left untouched)."
                : $"Killed {killed} bundled adb.exe process(es); shared ADB server was left untouched.");
            InvalidateDeviceCache();
            return string.Join(" ", parts);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    /// <summary>Wait until this app's bundled ADB processes have actually exited (file locks).</summary>
    public string WaitUntilProcessesExit(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Process[] leftover;
            try
            {
                leftover = Process.GetProcessesByName("adb");
            }
            catch
            {
                return "adb.exe gone.";
            }

            var owned = leftover.Where(IsBundledProcess).ToArray();
            foreach (var process in leftover)
            {
                process.Dispose();
            }

            if (owned.Length == 0)
            {
                return "Bundled adb.exe gone.";
            }

            Thread.Sleep(100);
        }

        return "Bundled adb.exe still present after wait.";
    }

    public IReadOnlyList<AdbDevice> ListDevices(bool force = false)
    {
        if (!force)
        {
            lock (_cacheLock)
            {
                if (_cachedDevices is not null
                    && DateTime.UtcNow - _cachedDevicesUtc < DevicesCache)
                {
                    return _cachedDevices;
                }
            }
        }

        Refresh();
        if (!IsAvailable)
        {
            lock (_cacheLock)
            {
                _cachedDevices = [];
                _cachedDevicesUtc = DateTime.UtcNow;
                return _cachedDevices;
            }
        }

        var output = Run("devices -l");
        var devices = new List<AdbDevice>();
        foreach (var raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var model = Field(parts, "model:");
            devices.Add(new AdbDevice
            {
                Serial = parts[0],
                State = parts[1],
                Model = string.IsNullOrWhiteSpace(model) ? null : model.Replace('_', ' '),
                Product = Field(parts, "product:"),
                DeviceName = Field(parts, "device:")
            });
        }

        lock (_cacheLock)
        {
            _cachedDevices = devices;
            _cachedDevicesUtc = DateTime.UtcNow;
            return devices;
        }
    }

    public void InvalidateDeviceCache()
    {
        lock (_cacheLock)
        {
            _cachedDevices = null;
            _cachedDevicesUtc = DateTime.MinValue;
            _cachedRuntime = null;
            _cachedRuntimeSerial = null;
            _cachedRuntimeUtc = DateTime.MinValue;
        }
    }

    public static bool LooksLikeWirelessSerial(string? serial) =>
        !string.IsNullOrWhiteSpace(serial) && serial.Contains(':', StringComparison.Ordinal);

    public static string FormatEndpoint(string host, int port)
    {
        host = host.Trim();
        if (host.Length == 0)
        {
            throw new ArgumentException("Enter the headset LAN IP (e.g. 192.168.1.40).", nameof(host));
        }

        // Strip accidental :port from the host field.
        var colon = host.IndexOf(':');
        if (colon >= 0)
        {
            if (colon > 0 && colon == host.LastIndexOf(':')
                && int.TryParse(host[(colon + 1)..], out var embeddedPort))
            {
                host = host[..colon];
                port = embeddedPort;
            }
            else
            {
                throw new ArgumentException("Enter the headset IP/host name and numeric port only.", nameof(host));
            }
        }

        if (host.Any(char.IsWhiteSpace) || host.IndexOfAny(['"', '\'', '`']) >= 0)
        {
            throw new ArgumentException("Enter only the headset IP/host name, without spaces, quotes, or extra ADB arguments.", nameof(host));
        }

        if (host.Length == 0)
        {
            throw new ArgumentException("Enter the headset LAN IP (e.g. 192.168.1.40).", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be 1–65535 (classic tcpip uses 5555).");
        }

        return $"{host}:{port}";
    }

    /// <summary>Connect over Wi‑Fi (<c>adb connect host:port</c>). Works after tcpip, Wireless debugging, or SideQuest on-headset ADB.</summary>
    public string ConnectWireless(string host, int port)
    {
        var endpoint = FormatEndpoint(host, port);
        var output = Run($"connect {endpoint}");
        InvalidateDeviceCache();
        if (output.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("failed to connect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Could not connect to {endpoint}. {TrimAdbNoise(output)} "
                + "Same Wi‑Fi as the PC? For classic mode: plug USB → Enable tcpip → unplug → Connect. "
                + "Or Pair with the Wireless debugging code, then Connect with the connect port. "
                + "SideQuest running on the headset can also open an ADB port — use that IP:port here.");
        }

        return output.Contains("already connected", StringComparison.OrdinalIgnoreCase)
            ? $"Already connected to {endpoint}."
            : $"Connected to {endpoint}. {TrimAdbNoise(output)}".Trim();
    }

    /// <summary>
    /// Connect, then (when headset-only is on) drop the session if it is a phone/tablet/emulator
    /// and disconnect other non-headset wireless devices ADB already listed.
    /// </summary>
    public string ConnectWirelessHeadset(string host, int port, HeadsetSettings settings)
    {
        var summary = ConnectWireless(host, port);
        if (settings.HeadsetOnlyWirelessAdb)
        {
            var rejected = RejectNonHeadsetWireless(FormatEndpoint(host, port));
            if (rejected is not null)
            {
                throw new InvalidOperationException(rejected);
            }

            // While ADB is paused for other devices, do not disconnect phones/TVs already on the bus.
            if (!settings.AdbWatcherPaused)
            {
                var swept = SweepNonHeadsetWireless(settings);
                if (!string.IsNullOrWhiteSpace(swept))
                {
                    summary = $"{summary} {swept}";
                }
            }
        }

        return summary;
    }

    /// <summary>
    /// Quest Wireless debugging: <c>adb pair host:pairingPort code</c>.
    /// Pairing port + 6-digit code come from Pair device with pairing code; then use Connect with the connect port.
    /// </summary>
    public string PairWireless(string host, int pairingPort, string pairingCode)
    {
        host = (host ?? string.Empty).Trim();
        pairingCode = (pairingCode ?? string.Empty).Trim().Replace(" ", "", StringComparison.Ordinal);
        if (host.Length == 0)
        {
            throw new InvalidOperationException("Enter the headset LAN IP first.");
        }

        if (pairingPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(pairingPort), "Pairing port must be 1–65535.");
        }

        if (pairingCode.Length is < 5 or > 8 || !pairingCode.All(char.IsDigit))
        {
            throw new InvalidOperationException("Enter the 6-digit pairing code from Wireless debugging → Pair device.");
        }

        var endpoint = FormatEndpoint(host, pairingPort);
        // adb pair IP:PORT CODE — code is a separate argv, not part of the endpoint.
        var output = Run($"pair {endpoint} {pairingCode}");
        InvalidateDeviceCache();

        if (output.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("error", StringComparison.OrdinalIgnoreCase)
            || output.Contains("incorrect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("refused", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Pairing failed for {endpoint}. {TrimAdbNoise(output)} "
                + "Open Pair device with pairing code again (port/code expire quickly), then retry.");
        }

        var trimmed = TrimAdbNoise(output);
        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Paired with {endpoint}. Now Connect using the Wireless debugging connect port (not the pairing port)."
            : $"Paired with {endpoint}. {trimmed} — next: Connect with the connect port shown under Wireless debugging.";
    }

    public string DisconnectWireless(string? host = null, int? port = null)
    {
        string output;
        if (!string.IsNullOrWhiteSpace(host) && port is int p)
        {
            var endpoint = FormatEndpoint(host, p);
            output = Run($"disconnect {endpoint}");
            InvalidateDeviceCache();
            return $"Disconnected {endpoint}. {TrimAdbNoise(output)}".Trim();
        }

        output = Run("disconnect");
        InvalidateDeviceCache();
        return $"Disconnected wireless ADB sessions. {TrimAdbNoise(output)}".Trim();
    }

    /// <summary>
    /// USB once: put the headset into classic wireless mode (<c>adb tcpip PORT</c>).
    /// Prefers a USB serial (no host:port) when both USB and wireless are listed.
    /// </summary>
    public string EnableTcpipMode(int port, out string? suggestedHost)
    {
        suggestedHost = null;
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be 1–65535.");
        }

        var usb = FindUsbHeadset()
                  ?? throw new InvalidOperationException(
                      "Plug the Quest in over USB (Developer Mode + authorize) before enabling tcpip.");

        Run($"-s {usb.Serial} tcpip {port}");
        suggestedHost = TryReadLanIp(usb.Serial);
        InvalidateDeviceCache();

        return suggestedHost is null
            ? $"TCP/IP mode on port {port} for {usb.Serial}. Unplug USB, enter the headset Wi‑Fi IP, then Connect."
            : $"TCP/IP mode on port {port} for {usb.Serial}. Suggested IP {suggestedHost} — unplug USB, then Connect.";
    }

    /// <summary>Quiet reconnect for the watcher — no throw; empty string if skipped or already online.</summary>
    public string TryAutoReconnect(HeadsetSettings settings)
    {
        if (!settings.WirelessAutoReconnect)
        {
            return string.Empty;
        }

        var endpoint = settings.WirelessEndpoint;
        if (endpoint is null)
        {
            return string.Empty;
        }

        if (FindQuest()?.IsReady == true)
        {
            return string.Empty;
        }

        try
        {
            var host = settings.WirelessHost!.Trim();
            return ConnectWirelessHeadset(host, settings.WirelessPort, settings);
        }
        catch (Exception ex)
        {
            return $"Wireless auto-reconnect: {ex.Message}";
        }
    }

    /// <summary>
    /// Disconnect wireless (IP:port) sessions that are not a VR headset.
    /// Does not touch USB devices. Leaves the saved endpoint alone while it is still unauthorized
    /// (Quest waiting for the debugging prompt).
    /// </summary>
    public string? SweepNonHeadsetWireless(HeadsetSettings settings)
    {
        if (!settings.HeadsetOnlyWirelessAdb)
        {
            return null;
        }

        var kept = settings.WirelessEndpoint;
        var dropped = new List<string>();
        foreach (var device in ListDevices(force: true))
        {
            if (!LooksLikeWirelessSerial(device.Serial))
            {
                continue;
            }

            var (isVr, probe) = Classify(device);
            if (isVr)
            {
                continue;
            }

            if (device.NeedsAuthorization && SameWirelessEndpoint(device.Serial, kept))
            {
                continue;
            }

            try
            {
                Run($"disconnect {device.Serial}");
                dropped.Add(VrHeadsetClassifier.DescribeIgnored(device, probe));
            }
            catch
            {
                // Best-effort — ADB may already have dropped the session.
            }
        }

        if (dropped.Count == 0)
        {
            return null;
        }

        InvalidateDeviceCache();
        return dropped.Count == 1
            ? $"Disconnected wireless ADB that is not a VR headset: {dropped[0]}"
            : $"Disconnected {dropped.Count} wireless ADB devices that are not VR headsets.";
    }

    private string? RejectNonHeadsetWireless(string endpoint)
    {
        var device = ListDevices(force: true)
            .FirstOrDefault(item => SameWirelessEndpoint(item.Serial, endpoint));
        if (device is null)
        {
            return null;
        }

        if (device.NeedsAuthorization)
        {
            return null;
        }

        var (isVr, probe) = Classify(device);
        if (isVr)
        {
            return null;
        }

        try
        {
            Run($"disconnect {device.Serial}");
        }
        catch
        {
            // still report
        }

        InvalidateDeviceCache();
        return $"Disconnected {endpoint} — {VrHeadsetClassifier.DescribeIgnored(device, probe)} "
               + "Wireless ADB stays headset-only; phones, tablets, and emulators are dropped.";
    }

    private static bool SameWirelessEndpoint(string serial, string? endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint)
        && string.Equals(serial.Trim(), endpoint.Trim(), StringComparison.OrdinalIgnoreCase);

    public AdbDevice? FindUsbHeadset()
    {
        var devices = ListDevices(force: true);
        return devices.FirstOrDefault(device => !LooksLikeWirelessSerial(device.Serial) && Classify(device).IsVr && device.IsReady)
               ?? devices.FirstOrDefault(device => !LooksLikeWirelessSerial(device.Serial) && device.NeedsAuthorization
                                                 && !VrHeadsetClassifier.IsObviousEmulator(device));
    }

    public string? TryReadLanIp(string serial)
    {
        try
        {
            foreach (var prop in new[] { "dhcp.wlan0.ipaddress", "dhcp.eth0.ipaddress" })
            {
                var value = GetProp(serial, prop);
                if (LooksLikeIpv4(value))
                {
                    return value;
                }
            }

            var route = Shell(serial, "ip -f inet addr show wlan0").Trim();
            var match = System.Text.RegularExpressions.Regex.Match(route, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
            if (match.Success && LooksLikeIpv4(match.Groups[1].Value))
            {
                return match.Groups[1].Value;
            }
        }
        catch
        {
            // optional hint only
        }

        return null;
    }

    private static bool LooksLikeIpv4(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && System.Net.IPAddress.TryParse(value, out var ip)
        && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

    private static string TrimAdbNoise(string text)
    {
        var line = text.Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? text.Trim();
        return line.Length > 160 ? line[..160] + "…" : line;
    }

    public AdbDevice? FindQuest()
    {
        var devices = ListDevices();
        return devices.FirstOrDefault(device => Classify(device).IsVr)
               // USB only: an unauthorized wireless phone must not be treated as a Quest.
               ?? devices.FirstOrDefault(device => device.NeedsAuthorization
                                                 && !LooksLikeWirelessSerial(device.Serial)
                                                 && !VrHeadsetClassifier.IsObviousEmulator(device));
    }

    public string? DescribeIgnoredDevices()
    {
        var ignored = ListDevices()
            .Select(device => (device, kind: Classify(device)))
            .Where(item => !item.kind.IsVr)
            .ToList();
        if (ignored.Count == 0)
        {
            return null;
        }

        return string.Join(" ", ignored.Select(item =>
            VrHeadsetClassifier.DescribeIgnored(item.device, item.kind.Probe)));
    }

    public string DescribeStatus()
    {
        Refresh();
        if (!IsAvailable)
        {
            return "Bundled ADB was not found. Rebuild the app so tools/platform-tools is copied next to the exe.";
        }

        var quest = FindQuest();
        if (quest is null)
        {
            var ignored = DescribeIgnoredDevices();
            var wirelessHint = " Or use Wireless ADB on the Headset page.";
            return ignored is null
                ? $"ADB ready. No VR headset connected. Plug in a {VrHeadsetClassifier.AllowedHeadsetList} with Developer Mode.{wirelessHint}"
                : $"No VR headset. {ignored}";
        }

        if (quest.NeedsAuthorization)
        {
            return $"Headset {quest.Serial} is unauthorized — accept the USB debugging prompt in the headset.";
        }

        if (!quest.IsReady)
        {
            return $"Headset {quest.Serial} state: {quest.State}.";
        }

        var model = quest.Model ?? GetProp(quest.Serial, "ro.product.model") ?? "VR headset";
        var transport = LooksLikeWirelessSerial(quest.Serial) ? "wireless" : "USB";
        return $"VR headset connected ({transport}): {model} ({quest.Serial}).";
    }

    public string DescribeCachedStatus()
    {
        IReadOnlyList<AdbDevice>? devices;
        DateTime cachedAt;
        lock (_cacheLock)
        {
            devices = _cachedDevices;
            cachedAt = _cachedDevicesUtc;
        }

        if (devices is null)
        {
            return IsAvailable
                ? "ADB ready. Status will refresh in the background."
                : "ADB not checked yet.";
        }

        var age = DateTime.UtcNow - cachedAt;
        var suffix = age > TimeSpan.FromSeconds(10)
            ? $" Last checked {(int)age.TotalSeconds}s ago."
            : string.Empty;
        var quest = devices.FirstOrDefault(device => VrHeadsetClassifier.IsAllowedVrHeadset(device))
                    ?? devices.FirstOrDefault(device =>
                        device.NeedsAuthorization
                        && !LooksLikeWirelessSerial(device.Serial)
                        && !VrHeadsetClassifier.IsObviousEmulator(device));

        if (quest is null)
        {
            var ignored = devices.FirstOrDefault(device => !VrHeadsetClassifier.IsAllowedVrHeadset(device));
            return ignored is null
                ? "ADB ready. No VR headset connected." + suffix
                : $"No VR headset. ADB sees {ignored.Serial} ({ignored.Model ?? ignored.State})." + suffix;
        }

        if (quest.NeedsAuthorization)
        {
            return $"Headset {quest.Serial} is unauthorized." + suffix;
        }

        var transport = LooksLikeWirelessSerial(quest.Serial) ? "wireless" : "USB";
        return $"VR headset connected ({transport}): {quest.Model ?? "Quest"} ({quest.Serial})." + suffix;
    }

    public HeadsetIdentity ReadIdentity(string? trustedSerial)
    {
        var devices = ListDevices();
        var classified = devices.Select(device => (device, kind: Classify(device))).ToList();
        var headset = classified.FirstOrDefault(item => item.kind.IsVr).device;
        if (headset is null)
        {
            var ignored = classified.FirstOrDefault().device;
            if (ignored is null)
            {
                return new HeadsetIdentity { State = "disconnected" };
            }

            var probe = classified[0].kind.Probe ?? Probe(ignored);
            return new HeadsetIdentity
            {
                AdbSerial = ignored.Serial,
                Serial = probe.Model is null ? ignored.Serial : GetProp(ignored.Serial, "ro.serialno") ?? ignored.Serial,
                Model = probe.Model ?? ignored.Model,
                Device = probe.Device ?? ignored.DeviceName,
                Manufacturer = probe.Manufacturer,
                AndroidVersion = ignored.IsReady ? GetProp(ignored.Serial, "ro.build.version.release") : null,
                BuildDisplay = ignored.IsReady ? GetProp(ignored.Serial, "ro.build.display.id") : null,
                Fingerprint = probe.Fingerprint,
                State = "ignored",
                IsReady = false,
                IsVrHeadset = false,
                IsIgnored = true,
                IgnoreReason = VrHeadsetClassifier.DescribeIgnored(ignored, probe),
                IsTrusted = false,
                IsRogue = false
            };
        }

        var hardwareSerial = headset.IsReady
            ? GetProp(headset.Serial, "ro.serialno") ?? GetProp(headset.Serial, "ro.boot.serialno")
            : null;
        var model = headset.IsReady
            ? GetProp(headset.Serial, "ro.product.model") ?? headset.Model
            : headset.Model;
        var identitySerial = hardwareSerial ?? headset.Serial;
        var trusted = !string.IsNullOrWhiteSpace(trustedSerial)
                      && !VrHeadsetClassifier.LooksLikeNonHeadsetSerial(trustedSerial)
                      && string.Equals(trustedSerial, identitySerial, StringComparison.OrdinalIgnoreCase);
        var rogue = !string.IsNullOrWhiteSpace(trustedSerial)
                    && !VrHeadsetClassifier.LooksLikeNonHeadsetSerial(trustedSerial)
                    && !trusted
                    && headset.IsReady;

        return new HeadsetIdentity
        {
            AdbSerial = headset.Serial,
            Serial = identitySerial,
            Model = model,
            Device = headset.IsReady ? GetProp(headset.Serial, "ro.product.device") : headset.DeviceName,
            Manufacturer = headset.IsReady ? GetProp(headset.Serial, "ro.product.manufacturer") : null,
            AndroidVersion = headset.IsReady ? GetProp(headset.Serial, "ro.build.version.release") : null,
            BuildDisplay = headset.IsReady ? GetProp(headset.Serial, "ro.build.display.id") : null,
            Fingerprint = headset.IsReady ? GetProp(headset.Serial, "ro.build.fingerprint") : null,
            State = headset.State,
            IsReady = headset.IsReady,
            IsVrHeadset = true,
            IsTrusted = trusted,
            IsRogue = rogue,
            Runtime = headset.IsReady ? ReadRuntimeStatus(headset.Serial) : null
        };
    }

    /// <summary>Battery / charge / Wi‑Fi via dumpsys (cached ~20s).</summary>
    public HeadsetRuntimeStatus ReadRuntimeStatus(string serial, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return new HeadsetRuntimeStatus { Available = false, Error = "no serial" };
        }

        lock (_cacheLock)
        {
            if (!force
                && _cachedRuntime is not null
                && string.Equals(_cachedRuntimeSerial, serial, StringComparison.OrdinalIgnoreCase)
                && DateTime.UtcNow - _cachedRuntimeUtc < RuntimeCache)
            {
                return _cachedRuntime;
            }
        }

        HeadsetRuntimeStatus status;
        try
        {
            var battery = Shell(serial, "dumpsys battery");
            var wifi = Shell(serial, "dumpsys wifi");
            status = ParseRuntimeStatus(battery, wifi);
        }
        catch (Exception ex)
        {
            status = new HeadsetRuntimeStatus { Available = false, Error = ex.Message };
        }

        lock (_cacheLock)
        {
            _cachedRuntime = status;
            _cachedRuntimeSerial = serial;
            _cachedRuntimeUtc = DateTime.UtcNow;
        }

        return status;
    }

    private static HeadsetRuntimeStatus ParseRuntimeStatus(string batteryDump, string wifiDump)
    {
        int? level = null;
        bool? charging = null;
        string? chargeStatus = null;

        foreach (var raw in batteryDump.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("level:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line["level:".Length..].Trim(), out var pct))
            {
                level = Math.Clamp(pct, 0, 100);
            }
            else if (line.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
            {
                var code = line["status:".Length..].Trim();
                // Android BatteryManager: 2=charging, 3=discharging, 5=full, 4=not charging
                chargeStatus = code switch
                {
                    "2" => "Charging",
                    "3" => "Discharging",
                    "4" => "Not charging",
                    "5" => "Full",
                    _ => code
                };
                charging = code is "2" or "5";
            }
            else if (line.StartsWith("AC powered:", StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("USB powered:", StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("Wireless powered:", StringComparison.OrdinalIgnoreCase))
            {
                if (line.EndsWith("true", StringComparison.OrdinalIgnoreCase))
                {
                    charging = true;
                }
            }
        }

        string? ssid = null;
        int? rssi = null;
        foreach (var raw in wifiDump.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (ssid is null
                && (line.Contains("SSID:", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("ssid:", StringComparison.OrdinalIgnoreCase)))
            {
                var idx = line.IndexOf("SSID:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    idx = line.IndexOf("ssid:", StringComparison.OrdinalIgnoreCase);
                }

                if (idx >= 0)
                {
                    var value = line[(idx + 5)..].Trim().Trim(',', ' ', '"');
                    if (!string.IsNullOrWhiteSpace(value)
                        && !value.Equals("<unknown ssid>", StringComparison.OrdinalIgnoreCase)
                        && !value.Equals("0x", StringComparison.OrdinalIgnoreCase)
                        && !value.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        ssid = value;
                    }
                }
            }

            if (rssi is null && line.Contains("RSSI:", StringComparison.OrdinalIgnoreCase))
            {
                var idx = line.IndexOf("RSSI:", StringComparison.OrdinalIgnoreCase);
                var token = line[(idx + 5)..].Trim().Split(' ', ',', ';')[0];
                if (int.TryParse(token, out var r))
                {
                    rssi = r;
                }
            }
        }

        var available = level is not null || ssid is not null || rssi is not null;
        return new HeadsetRuntimeStatus
        {
            Available = available,
            BatteryPercent = level,
            IsCharging = charging,
            ChargeStatus = chargeStatus,
            WifiSsid = ssid,
            WifiRssi = rssi,
            Error = available ? null : "battery/Wi‑Fi dumpsys returned no fields"
        };
    }

    public string? GetProp(string serial, string name)
    {
        var value = Run($"-s {serial} shell getprop {name}").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string SetProp(string serial, string name, string value)
    {
        Run($"-s {serial} shell setprop {name} {value}");
        return $"{name}={value}";
    }

    public string Shell(string serial, string command) => Run($"-s {serial} shell {command}");

    public string SendText(string serial, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "Nothing to send.";
        }

        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace(" ", "%s")
            .Replace("'", "\\'")
            .Replace("&", "\\&")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace("|", "\\|")
            .Replace(";", "\\;");
        Run($"-s {serial} shell input text \"{escaped}\"");
        return "Sent text to the focused headset field.";
    }

    public string CapturePngScreenshot(string serial, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            throw new ArgumentException("ADB serial is required.", nameof(serial));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Screenshot output path is required.", nameof(outputPath));
        }

        var folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        RunToFile($"-s {serial} exec-out screencap -p", outputPath);
        if (!IsValidPngFile(outputPath))
        {
            TryDeleteFile(outputPath);
            throw new InvalidOperationException("ADB screenshot did not produce a valid PNG.");
        }

        return outputPath;
    }

    private (bool IsVr, DeviceProbe? Probe) Classify(AdbDevice device)
    {
        if (VrHeadsetClassifier.IsObviousEmulator(device))
        {
            return (false, null);
        }

        if (VrHeadsetClassifier.IsAllowedVrHeadset(device))
        {
            return (true, null);
        }

        if (!device.IsReady)
        {
            return (false, null);
        }

        var probe = Probe(device);
        return (VrHeadsetClassifier.IsAllowedVrHeadset(device, probe), probe);
    }

    private DeviceProbe Probe(AdbDevice device)
    {
        var qemu = GetProp(device.Serial, "ro.kernel.qemu");
        var hardware = GetProp(device.Serial, "ro.hardware");
        var fingerprint = GetProp(device.Serial, "ro.build.fingerprint");
        var emulator = string.Equals(qemu, "1", StringComparison.OrdinalIgnoreCase)
                       || ContainsIgnore(hardware, "goldfish")
                       || ContainsIgnore(hardware, "ranchu")
                       || ContainsIgnore(fingerprint, "sdk_google")
                       || ContainsIgnore(fingerprint, "sdk_gphone");

        return new DeviceProbe
        {
            Model = GetProp(device.Serial, "ro.product.model") ?? device.Model,
            Device = GetProp(device.Serial, "ro.product.device") ?? device.DeviceName,
            Manufacturer = GetProp(device.Serial, "ro.product.manufacturer"),
            Brand = GetProp(device.Serial, "ro.product.brand"),
            Hardware = hardware,
            Fingerprint = fingerprint,
            OculusDevice = GetProp(device.Serial, "ro.oculus.device"),
            IsEmulator = emulator
        };
    }

    private static string? Field(string[] parts, string prefix) =>
        parts.FirstOrDefault(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    private static bool ContainsIgnore(string? value, string token) =>
        value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true;

    private static string? FindAdb()
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
        yield return Path.Combine(AppContext.BaseDirectory, "adb.exe");

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        yield return Path.Combine(local, "Android", "Sdk", "platform-tools", "adb.exe");
        yield return Path.Combine(user, "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe");
        yield return Path.Combine(pf, "Android", "android-sdk", "platform-tools", "adb.exe");
        yield return Path.Combine(pf86, "Android", "android-sdk", "platform-tools", "adb.exe");
        yield return Path.Combine(local, "Android", "Sdk", "platform-tools", "adb.exe");

        foreach (var root in new[]
                 {
                     Path.Combine(local, "Programs", "SideQuest"),
                     Path.Combine(pf, "SideQuest"),
                     Path.Combine(local, "sidequest")
                 })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            string[] hits;
            try
            {
                hits = Directory.GetFiles(root, "adb.exe", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var hit in hits)
            {
                yield return hit;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return Path.Combine(folder.Trim('"'), "adb.exe");
        }
    }

    private string Run(string arguments)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("ADB was not found.");
        }

        if (!_commandGate.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"ADB command queue was busy for 30s: adb {arguments}");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(AdbPath) ?? AppContext.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("ADB process could not be started.");
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(20_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // hung adb.exe
                }

                try
                {
                    process.WaitForExit(3_000);
                }
                catch
                {
                    // ignore
                }

                throw new TimeoutException($"ADB timed out after 20s: adb {arguments}");
            }

            if (!Task.WaitAll([stdout, stderr], 3_000))
            {
                throw new TimeoutException($"ADB output read timed out: adb {arguments}");
            }

            var output = stdout.Result;
            var error = stderr.Result;
            var combined = (output + Environment.NewLine + error).Trim();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(combined)
                        ? $"ADB exited with code {process.ExitCode}: adb {arguments}"
                        : $"ADB exited with code {process.ExitCode}: {combined}");
            }

            return combined;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private void RunToFile(string arguments, string outputPath)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("ADB was not found.");
        }

        if (!_commandGate.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"ADB command queue was busy for 30s: adb {arguments}");
        }

        var tempPath = outputPath + ".tmp";
        try
        {
            TryDeleteFile(tempPath);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(AdbPath) ?? AppContext.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            using var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);

            if (!process.Start())
            {
                throw new InvalidOperationException("ADB process could not be started.");
            }

            var stdout = process.StandardOutput.BaseStream.CopyToAsync(output);
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(20_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // hung adb.exe
                }

                try
                {
                    process.WaitForExit(3_000);
                }
                catch
                {
                    // ignore
                }

                throw new TimeoutException($"ADB timed out after 20s: adb {arguments}");
            }

            if (!Task.WaitAll([stdout, stderr], 3_000))
            {
                throw new TimeoutException($"ADB output read timed out: adb {arguments}");
            }

            output.Flush(flushToDisk: true);
            var error = stderr.Result.Trim();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"ADB exited with code {process.ExitCode}: adb {arguments}"
                        : $"ADB exited with code {process.ExitCode}: {error}");
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    internal static bool IsValidPngFile(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < 8)
            {
                return false;
            }

            var expected = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var actual = new byte[expected.Length];
            using var stream = File.OpenRead(path);
            return stream.Read(actual, 0, actual.Length) == actual.Length
                   && actual.SequenceEqual(expected);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private static bool IsBundledProcess(Process process)
    {
        try
        {
            var executable = process.MainModule?.FileName;
            return IsBundledAdbExecutable(executable);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsBundledAdbExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(executablePath);
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            var packagedCandidates = new[]
            {
                Path.Combine(baseDir, "platform-tools", "adb.exe"),
                Path.Combine(baseDir, "adb.exe")
            };

            return packagedCandidates.Any(candidate =>
                string.Equals(
                    fullPath,
                    Path.GetFullPath(candidate),
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
