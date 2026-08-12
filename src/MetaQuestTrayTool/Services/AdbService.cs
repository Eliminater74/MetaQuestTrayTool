using System.Diagnostics;
using System.IO;
using System.Text;

namespace MetaQuestTrayTool.Services;

public sealed class AdbDevice
{
    public required string Serial { get; init; }
    public required string State { get; init; }
    public string? Model { get; init; }

    public bool IsReady => State.Equals("device", StringComparison.OrdinalIgnoreCase);
    public bool NeedsAuthorization => State.Equals("unauthorized", StringComparison.OrdinalIgnoreCase);
}

public sealed class AdbService
{
    public string? AdbPath { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(AdbPath) && File.Exists(AdbPath);

    public void Refresh()
    {
        if (!string.IsNullOrWhiteSpace(AdbPath) && File.Exists(AdbPath))
        {
            return;
        }

        AdbPath = FindAdb();
    }

    public IReadOnlyList<AdbDevice> ListDevices()
    {
        Refresh();
        if (!IsAvailable)
        {
            return [];
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

            var model = parts.FirstOrDefault(part => part.StartsWith("model:", StringComparison.OrdinalIgnoreCase))?["model:".Length..];
            devices.Add(new AdbDevice
            {
                Serial = parts[0],
                State = parts[1],
                Model = string.IsNullOrWhiteSpace(model) ? null : model.Replace('_', ' ')
            });
        }

        return devices;
    }

    public AdbDevice? FindQuest()
    {
        var devices = ListDevices();
        return devices.FirstOrDefault(IsLikelyQuest) ?? devices.FirstOrDefault();
    }

    public string DescribeStatus()
    {
        Refresh();
        if (!IsAvailable)
        {
            return "ADB was not found. Install Android Platform-Tools or SideQuest (we reuse its adb.exe).";
        }

        var quest = FindQuest();
        if (quest is null)
        {
            return $"ADB: {AdbPath}. No headset connected. Enable Developer Mode and plug in USB (or wireless ADB).";
        }

        if (quest.NeedsAuthorization)
        {
            return $"Headset {quest.Serial} is unauthorized — accept the USB debugging prompt in the headset.";
        }

        if (!quest.IsReady)
        {
            return $"Headset {quest.Serial} state: {quest.State}.";
        }

        var model = quest.Model ?? GetProp(quest.Serial, "ro.product.model") ?? "Quest";
        return $"Headset connected: {model} ({quest.Serial}).";
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

    private static bool IsLikelyQuest(AdbDevice device)
    {
        var blob = $"{device.Model} {device.Serial}";
        return blob.Contains("quest", StringComparison.OrdinalIgnoreCase)
               || blob.Contains("oculus", StringComparison.OrdinalIgnoreCase)
               || blob.Contains("hollywood", StringComparison.OrdinalIgnoreCase)
               || blob.Contains("eureka", StringComparison.OrdinalIgnoreCase)
               || blob.Contains("pacific", StringComparison.OrdinalIgnoreCase)
               || blob.Contains("seacliff", StringComparison.OrdinalIgnoreCase);
    }

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
