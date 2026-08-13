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

    private IReadOnlyList<AdbDevice>? _cachedDevices;
    private DateTime _cachedDevicesUtc = DateTime.MinValue;
    private static readonly TimeSpan DevicesCache = TimeSpan.FromSeconds(2);

    public void Refresh()
    {
        if (!string.IsNullOrWhiteSpace(AdbPath) && File.Exists(AdbPath))
        {
            return;
        }

        AdbPath = FindAdb();
    }

    /// <summary>
    /// Stops the ADB server and force-kills leftover <c>adb.exe</c> processes so Setup can
    /// replace <c>platform-tools</c> (the server often keeps files locked even with no headset).
    /// </summary>
    public string KillServerForUpdate()
    {
        Refresh();
        var parts = new List<string>();

        if (IsAvailable)
        {
            try
            {
                Run("kill-server");
                parts.Add("adb kill-server ok.");
            }
            catch (Exception ex)
            {
                parts.Add($"adb kill-server: {ex.Message}");
            }
        }

        var killed = 0;
        foreach (var process in Process.GetProcessesByName("adb"))
        {
            try
            {
                using (process)
                {
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
            ? "No adb.exe processes left."
            : $"Killed {killed} adb.exe process(es).");
        _cachedDevices = null;
        _cachedDevicesUtc = DateTime.MinValue;
        return string.Join(" ", parts);
    }

    public IReadOnlyList<AdbDevice> ListDevices(bool force = false)
    {
        if (!force
            && _cachedDevices is not null
            && DateTime.UtcNow - _cachedDevicesUtc < DevicesCache)
        {
            return _cachedDevices;
        }

        Refresh();
        if (!IsAvailable)
        {
            _cachedDevices = [];
            _cachedDevicesUtc = DateTime.UtcNow;
            return _cachedDevices;
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

        _cachedDevices = devices;
        _cachedDevicesUtc = DateTime.UtcNow;
        return devices;
    }

    public AdbDevice? FindQuest()
    {
        var devices = ListDevices();
        return devices.FirstOrDefault(device => Classify(device).IsVr)
               ?? devices.FirstOrDefault(device => device.NeedsAuthorization
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
            return ignored is null
                ? $"ADB ready. No VR headset connected. Plug in a {VrHeadsetClassifier.AllowedHeadsetList} with Developer Mode."
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
        return $"VR headset connected: {model} ({quest.Serial}).";
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
            IsRogue = rogue
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

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(20_000);
        var combined = (output + Environment.NewLine + error).Trim();
        if (process.ExitCode != 0 && combined.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(combined);
        }

        return combined;
    }
}
