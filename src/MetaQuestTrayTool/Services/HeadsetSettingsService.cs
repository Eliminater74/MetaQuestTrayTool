using System.Globalization;
using System.IO;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed record HeadsetScreenshotResult(
    string FilePath,
    string Model,
    string Transport,
    long Bytes)
{
    public string Summary => $"Saved {Model} screenshot ({Transport}, {FormatBytes(Bytes)}) to {FilePath}.";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return (bytes / 1024d / 1024d).ToString("0.0 MB", CultureInfo.InvariantCulture);
        }

        if (bytes >= 1024)
        {
            return (bytes / 1024d).ToString("0.0 KB", CultureInfo.InvariantCulture);
        }

        return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
    }
}

public sealed class HeadsetSettingsService
{
    private readonly AdbService _adb;

    public HeadsetSettingsService(AdbService adb)
    {
        _adb = adb;
    }

    public HeadsetIdentity ReadIdentity(HeadsetSettings settings)
    {
        ForgetInvalidTrust(settings);
        return _adb.ReadIdentity(settings.TrustedSerial);
    }

    public string TrustCurrentHeadset(HeadsetSettings settings)
    {
        var identity = ReadIdentity(settings);
        if (identity.IsIgnored)
        {
            throw new InvalidOperationException(identity.IgnoreReason
                ?? "That ADB device is not a VR headset. Phones, tablets, and emulators cannot be trusted.");
        }

        if (!identity.IsReady || !identity.IsVrHeadset || string.IsNullOrWhiteSpace(identity.Serial))
        {
            throw new InvalidOperationException(
                $"Connect a VR headset ({VrHeadsetClassifier.AllowedHeadsetList}) with USB debugging authorized first.");
        }

        settings.TrustedSerial = identity.Serial;
        settings.TrustedModel = identity.Model;
        settings.RequireTrustedHeadset = true;
        return $"Trusted VR headset {identity.Model ?? "headset"} ({identity.Serial}). Commands will not run on phones, tablets, emulators, or any other device.";
    }

    public string Apply(HeadsetSettings settings, IReadOnlyList<string>? extraAdb = null)
    {
        var quest = RequireReadyHeadset(settings);
        var applied = new List<string>();

        switch (settings.CpuGpuLevel)
        {
            case HeadsetCpuGpuLevel.Level2:
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.cpuLevel", "2"));
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.gpuLevel", "2"));
                break;
            case HeadsetCpuGpuLevel.Level4:
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.cpuLevel", "4"));
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.gpuLevel", "4"));
                break;
        }

        if (TryTextureSize(settings.TextureSize, out var width, out var height))
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.textureWidth", width.ToString()));
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.textureHeight", height.ToString()));
        }

        var refresh = settings.RefreshRate switch
        {
            HeadsetRefreshRate.Hz60 => 60,
            HeadsetRefreshRate.Hz72 => 72,
            HeadsetRefreshRate.Hz80 => 80,
            HeadsetRefreshRate.Hz90 => 90,
            HeadsetRefreshRate.Hz120 => 120,
            _ => (int?)null
        };
        if (refresh is not null)
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.refreshRate", refresh.Value.ToString()));
        }

        var ffr = settings.Ffr switch
        {
            HeadsetFfrLevel.Off => 0,
            HeadsetFfrLevel.Low => 1,
            HeadsetFfrLevel.Medium => 2,
            HeadsetFfrLevel.High => 3,
            HeadsetFfrLevel.HighTop => 4,
            _ => (int?)null
        };
        if (ffr is not null)
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.foveation.level", ffr.Value.ToString()));
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.foveation.dynamic", "0"));
        }

        switch (settings.ChromaticAberration)
        {
            case HeadsetChromaMode.On:
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.forceChroma", "1"));
                break;
            case HeadsetChromaMode.Off:
                applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.forceChroma", "0"));
                break;
        }

        if (TryCaptureSize(settings.CaptureSize, out var capW, out var capH))
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.capture.width", capW.ToString()));
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.capture.height", capH.ToString()));
        }

        var fps = settings.CaptureFps switch
        {
            HeadsetCaptureFps.Fps24 => 24,
            HeadsetCaptureFps.Fps30 => 30,
            HeadsetCaptureFps.Fps60 => 60,
            _ => (int?)null
        };
        if (fps is not null)
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.capture.fps", fps.Value.ToString()));
        }

        var bitrate = settings.CaptureBitrate switch
        {
            HeadsetCaptureBitrate.Mbps5 => 5_000_000,
            HeadsetCaptureBitrate.Mbps10 => 10_000_000,
            HeadsetCaptureBitrate.Mbps15 => 15_000_000,
            HeadsetCaptureBitrate.Mbps20 => 20_000_000,
            _ => (int?)null
        };
        if (bitrate is not null)
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.capture.bitrate", bitrate.Value.ToString()));
        }

        applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.fullRateCapture", settings.FullRateCapture ? "1" : "0"));
        if (settings.StereoCapture)
        {
            applied.Add(_adb.SetProp(quest.Serial, "debug.oculus.capture.eye", "1"));
        }

        var extras = extraAdb ?? settings.CustomAdbCommands;
        foreach (var line in extras)
        {
            var shell = line.Trim();
            if (shell.Length == 0 || shell.StartsWith('#'))
            {
                continue;
            }

            if (shell.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
            {
                var idx = shell.IndexOf("shell ", StringComparison.OrdinalIgnoreCase);
                shell = idx >= 0 ? shell[(idx + 6)..].Trim() : shell[4..].Trim();
            }

            if (shell.Length > 0)
            {
                _adb.Shell(quest.Serial, shell);
                applied.Add(shell);
            }
        }

        var label = quest.Model ?? quest.Serial;
        return applied.Count == 0
            ? $"Headset {label} connected — all headset overrides are Device/App default (nothing to push)."
            : $"Applied {applied.Count} headset ADB command(s) on {label}.";
    }

    public string SetProximitySensor(bool enabled, HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        var action = enabled
            ? "com.oculus.vrpowermanager.prox_open"
            : "com.oculus.vrpowermanager.prox_close";
        _adb.Shell(quest.Serial, $"am broadcast -a {action}");
        return enabled
            ? "Proximity sensor enabled (headset can sleep when removed)."
            : "Proximity sensor disabled (headset stays awake).";
    }

    public string SetGuardianPaused(bool paused, HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        _adb.SetProp(quest.Serial, "debug.oculus.guardian_pause", paused ? "1" : "0");
        return paused ? "Guardian paused." : "Guardian enabled.";
    }

    public string SendText(string text, HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        return _adb.SendText(quest.Serial, text);
    }

    public HeadsetScreenshotResult CaptureScreenshot(HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        var model = quest.Model ?? _adb.GetProp(quest.Serial, "ro.product.model") ?? "Quest";
        var outputPath = CreateScreenshotPath(DateTimeOffset.Now, model);
        _adb.CapturePngScreenshot(quest.Serial, outputPath);
        var transport = AdbService.LooksLikeWirelessSerial(quest.Serial) ? "wireless" : "USB";
        return new HeadsetScreenshotResult(
            outputPath,
            model,
            transport,
            new FileInfo(outputPath).Length);
    }

    private void ForgetInvalidTrust(HeadsetSettings settings)
    {
        var model = settings.TrustedModel ?? string.Empty;
        if (VrHeadsetClassifier.LooksLikeNonHeadsetSerial(settings.TrustedSerial)
            || model.Contains("sdk_google", StringComparison.OrdinalIgnoreCase)
            || model.Contains("emulator", StringComparison.OrdinalIgnoreCase))
        {
            settings.TrustedSerial = null;
            settings.TrustedModel = null;
        }
    }

    private AdbDevice RequireReadyHeadset(HeadsetSettings settings)
    {
        ForgetInvalidTrust(settings);
        var quest = _adb.FindQuest();
        if (quest is null)
        {
            var ignored = _adb.DescribeIgnoredDevices();
            throw new InvalidOperationException(ignored is null
                ? $"No VR headset was found over ADB. Connect a {VrHeadsetClassifier.AllowedHeadsetList} with Developer Mode."
                : ignored);
        }

        if (quest.NeedsAuthorization)
        {
            throw new InvalidOperationException("Headset USB debugging is not authorized. Accept the prompt inside the headset.");
        }

        if (!quest.IsReady)
        {
            throw new InvalidOperationException($"Headset ADB state is '{quest.State}'.");
        }

        if (settings.RequireTrustedHeadset && string.IsNullOrWhiteSpace(settings.TrustedSerial))
        {
            TrustCurrentHeadset(settings);
        }

        if (settings.RequireTrustedHeadset && !string.IsNullOrWhiteSpace(settings.TrustedSerial))
        {
            var identity = _adb.ReadIdentity(settings.TrustedSerial);
            if (identity.IsRogue)
            {
                throw new InvalidOperationException(
                    $"Blocked untrusted headset {identity.Model} ({identity.Serial}). Trusted device is {settings.TrustedModel} ({settings.TrustedSerial}).");
            }
        }

        return quest;
    }

    private static string CreateScreenshotPath(DateTimeOffset capturedAt, string? model)
    {
        Directory.CreateDirectory(AppPaths.ScreenshotsDirectory);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var fileName = BuildScreenshotFileName(
                capturedAt,
                model,
                attempt == 0 ? null : attempt + 1);
            var path = Path.Combine(AppPaths.ScreenshotsDirectory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(
            AppPaths.ScreenshotsDirectory,
            BuildScreenshotFileName(capturedAt, model, duplicateIndex: null)
                .Replace(".png", "-" + Guid.NewGuid().ToString("N")[..8] + ".png", StringComparison.Ordinal));
    }

    internal static string BuildScreenshotFileName(
        DateTimeOffset capturedAt,
        string? model,
        int? duplicateIndex = null)
    {
        var timestamp = capturedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = duplicateIndex is null ? string.Empty : "-" + duplicateIndex.Value.ToString(CultureInfo.InvariantCulture);
        return $"QuestScreenshot-{timestamp}-{SanitizeFileToken(model)}{suffix}.png";
    }

    private static string SanitizeFileToken(string? value)
    {
        var token = string.IsNullOrWhiteSpace(value) ? "Quest" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = token
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();
        var clean = new string(chars);
        while (clean.Contains("--", StringComparison.Ordinal))
        {
            clean = clean.Replace("--", "-", StringComparison.Ordinal);
        }

        clean = clean.Trim('-');
        if (clean.Length == 0)
        {
            return "Quest";
        }

        return clean.Length <= 40 ? clean : clean[..40].Trim('-');
    }

    private static bool TryTextureSize(HeadsetTexturePreset preset, out int width, out int height)
    {
        (width, height) = preset switch
        {
            HeadsetTexturePreset.Quest1 => (1216, 1344),
            HeadsetTexturePreset.Quest2 => (1440, 1584),
            HeadsetTexturePreset.Quest3 => (1680, 1760),
            HeadsetTexturePreset.Square512 => (512, 512),
            HeadsetTexturePreset.Square768 => (768, 768),
            HeadsetTexturePreset.Square1024 => (1024, 1024),
            HeadsetTexturePreset.Square1280 => (1280, 1280),
            HeadsetTexturePreset.Square1536 => (1536, 1536),
            HeadsetTexturePreset.Square2048 => (2048, 2048),
            HeadsetTexturePreset.Square2560 => (2560, 2560),
            HeadsetTexturePreset.Square3072 => (3072, 3072),
            _ => (0, 0)
        };
        return width > 0;
    }

    private static bool TryCaptureSize(HeadsetCaptureSize size, out int width, out int height)
    {
        (width, height) = size switch
        {
            HeadsetCaptureSize.Size640x480 => (640, 480),
            HeadsetCaptureSize.Size1280x720 => (1280, 720),
            HeadsetCaptureSize.Size1920x1080 => (1920, 1080),
            HeadsetCaptureSize.Size1024x1024 => (1024, 1024),
            HeadsetCaptureSize.Size1600x1600 => (1600, 1600),
            _ => (0, 0)
        };
        return width > 0;
    }
}
