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

public sealed record HeadsetRecordingDownloadResult(
    string FilePath,
    string RemotePath,
    long Bytes)
{
    public string Summary => $"Downloaded latest headset recording ({FormatBytes(Bytes)}) to {FilePath}. Source: {RemotePath}.";

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

internal sealed record HeadsetRecordingCandidate(
    long ModifiedUnixSeconds,
    long Bytes,
    string RemotePath);

public sealed class HeadsetSettingsService
{
    private static readonly string[] RecordingDirectories =
    [
        "/sdcard/Oculus/VideoShots",
        "/sdcard/Movies",
        "/sdcard/Movies/Oculus",
        "/sdcard/DCIM/Oculus"
    ];

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

    internal static IReadOnlyDictionary<string, string> FoveationOverrides(HeadsetSettings settings)
    {
        var mode = settings.EffectiveFoveationMode;
        if (!Enum.IsDefined(mode)) throw new InvalidOperationException("Unknown foveation mode.");
        if (mode == HeadsetFoveationMode.AppDefault) return new Dictionary<string, string>();
        if (mode == HeadsetFoveationMode.Dynamic)
            return new Dictionary<string, string> { ["debug.oculus.foveation.dynamic"] = "1" };
        var level = settings.Ffr == HeadsetFfrLevel.DeviceDefault ? 0 : (int)settings.Ffr - 1;
        if (level is < 0 or > 4) throw new InvalidOperationException("Unknown fixed foveation level.");
        return new Dictionary<string, string>
        {
            ["debug.oculus.foveation.dynamic"] = "0",
            ["debug.oculus.foveation.level"] = level.ToString(CultureInfo.InvariantCulture)
        };
    }

    internal static IReadOnlyDictionary<string, string> PerformanceOverrides(HeadsetSettings settings)
    {
        var result = new Dictionary<string, string>();
        foreach (var (name, level) in new[] { ("cpuLevel", settings.EffectiveCpuLevel), ("gpuLevel", settings.EffectiveGpuLevel) })
        {
            if (!Enum.IsDefined(level)) throw new InvalidOperationException("CPU/GPU levels must be App Default or 0-4.");
            if (level != HeadsetPerformanceLevel.AppDefault)
                result["debug.oculus." + name] = ((int)level).ToString(CultureInfo.InvariantCulture);
        }
        return result;
    }

    internal static IReadOnlyDictionary<string, string> ExperimentalOverrides(HeadsetSettings settings, string? model)
    {
        var result = new Dictionary<string, string>();
        AddExperimentalBool(
            result,
            settings.LocalDimming,
            "debug.oculus.localDimming",
            "Local dimming is only exposed for Quest Pro.",
            HeadsetCapabilities.SupportsLocalDimming(model));
        AddExperimentalBool(
            result,
            settings.SubsampledFoveation,
            "debug.oculus.foveation.subsampled",
            null,
            supported: true);
        return result;
    }

    internal static IReadOnlyDictionary<string, string> DocumentedDefaultOverrides(string? model)
    {
        var result = new Dictionary<string, string>
        {
            ["debug.oculus.capture.width"] = "1024",
            ["debug.oculus.capture.height"] = "1024",
            ["debug.oculus.capture.bitrate"] = "5000000",
            ["debug.oculus.fullRateCapture"] = "0",
            ["debug.oculus.enableVideoCapture"] = "0"
        };

        if (TryDefaultTextureSize(model, out var width, out var height))
        {
            result["debug.oculus.textureWidth"] = width.ToString(CultureInfo.InvariantCulture);
            result["debug.oculus.textureHeight"] = height.ToString(CultureInfo.InvariantCulture);
            result["debug.oculus.refreshRate"] = "72";
        }

        return result;
    }

    public string SetRecording(HeadsetSettings settings, bool enabled)
    {
        var quest = RequireReadyHeadset(settings);
        _adb.SetProp(quest.Serial, "debug.oculus.enableVideoCapture", enabled ? "1" : "0");
        var value = _adb.GetProp(quest.Serial, "debug.oculus.enableVideoCapture");
        if (value != (enabled ? "1" : "0"))
            throw new InvalidOperationException("Recording request could not be verified. Check capture status inside the headset.");
        return enabled
            ? "Start recording requested and property verified. Confirm capture inside the headset; recordings stay on the headset."
            : "Stop recording requested and property verified. Check the headset for the saved recording.";
    }

    public string StopAndDownloadLatestRecording(HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        IReadOnlyList<HeadsetRecordingCandidate> ReadRecordings() =>
            ParseRecordingListing(_adb.Shell(quest.Serial, BuildRecordingListingCommand()));
        var beforeStop = ReadRecordings();
        var stop = SetRecording(settings, enabled: false);
        var recording = WaitForFinalizedRecording(beforeStop, ReadRecordings, Thread.Sleep);
        var download = DownloadRecording(quest.Serial, quest.Model, recording);
        return stop + Environment.NewLine + download.Summary;
    }

    internal static HeadsetRecordingCandidate WaitForFinalizedRecording(
        IReadOnlyList<HeadsetRecordingCandidate> beforeStop,
        Func<IReadOnlyList<HeadsetRecordingCandidate>> read,
        Action<TimeSpan> delay)
    {
        HeadsetRecordingCandidate? previous = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            delay(TimeSpan.FromSeconds(1));
            var newest = read().OrderByDescending(item => item.ModifiedUnixSeconds)
                .ThenByDescending(item => item.Bytes).FirstOrDefault();
            if (newest is { Bytes: > 1024, ModifiedUnixSeconds: > 0 }
                && !beforeStop.Contains(newest) && newest == previous)
                return newest;
            previous = newest;
        }

        throw new TimeoutException("The just-stopped recording has not appeared or stabilized yet. Wait for capture to finish in the headset, then use Download latest recording.");
    }

    public HeadsetRecordingDownloadResult DownloadLatestRecording(HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        var listing = _adb.Shell(quest.Serial, BuildRecordingListingCommand());
        var recording = ParseRecordingListing(listing)
            .OrderByDescending(item => item.ModifiedUnixSeconds)
            .ThenByDescending(item => item.Bytes)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No headset recordings were found. Stop recording in the headset first, then retry. Checked: "
                + string.Join(", ", RecordingDirectories) + ".");

        return DownloadRecording(quest.Serial, quest.Model, recording);
    }

    private HeadsetRecordingDownloadResult DownloadRecording(string serial, string? model, HeadsetRecordingCandidate recording)
    {
        var outputPath = CreateRecordingPath(DateTimeOffset.Now, model, recording.RemotePath);
        _adb.PullFile(serial, recording.RemotePath, outputPath);
        var file = new FileInfo(outputPath);
        if (!file.Exists || file.Length <= 1024)
        {
            TryDelete(outputPath);
            throw new InvalidOperationException("Downloaded recording was missing or too small to be a valid video.");
        }

        return new HeadsetRecordingDownloadResult(outputPath, recording.RemotePath, file.Length);
    }

    public string CapturePerformanceSample(HeadsetSettings settings, TimeSpan duration)
    {
        var quest = RequireReadyHeadset(settings);
        var output = _adb.LogcatForDuration(quest.Serial, duration);
        return HeadsetPerformanceSample.Parse(output, duration).ToDisplayText();
    }

    public string Apply(HeadsetSettings settings, IReadOnlyList<string>? extraAdb = null)
    {
        var quest = RequireReadyHeadset(settings);
        var applied = new List<string>();

        if (!HeadsetCapabilities.RefreshRates(quest.Model).Contains(settings.RefreshRate))
            throw new InvalidOperationException("The saved refresh rate is not supported by this headset model. Choose a supported rate or Device default.");
        var performance = PerformanceOverrides(settings);
        var foveation = FoveationOverrides(settings);
        var experimental = ExperimentalOverrides(settings, quest.Model);

        foreach (var (name, value) in performance)
            applied.Add(_adb.SetProp(quest.Serial, name, value));

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

        foreach (var (name, value) in foveation)
            applied.Add(_adb.SetProp(quest.Serial, name, value));

        foreach (var (name, value) in experimental)
            applied.Add(_adb.SetProp(quest.Serial, name, value));

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
            HeadsetCaptureBitrate.Mbps30 => 30_000_000,
            HeadsetCaptureBitrate.Mbps40 => 40_000_000,
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

    public string ResetDocumentedOverrides(HeadsetSettings settings)
    {
        var quest = RequireReadyHeadset(settings);
        var defaults = DocumentedDefaultOverrides(quest.Model);
        foreach (var (name, value) in defaults)
            _adb.SetProp(quest.Serial, name, value);

        var label = quest.Model ?? quest.Serial;
        return $"Reset {defaults.Count} documented ADB override(s) on {label}. Recording was stopped. Reboot the headset to clear CPU/GPU levels, foveation, chromatic aberration, local dimming, subsampled foveation, and any custom properties with no proven safe default.";
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
        var identity = ReadIdentity(settings);
        if (!identity.IsVrHeadset || !identity.IsReady || string.IsNullOrWhiteSpace(identity.AdbSerial))
            throw new InvalidOperationException("Connect a recognized VR headset and authorize USB debugging first.");
        if (settings.RequireTrustedHeadset)
        {
            if (string.IsNullOrWhiteSpace(settings.TrustedSerial))
            {
                // Trust exactly the identity selected above, never a second enumeration.
                settings.TrustedSerial = identity.Serial;
                settings.TrustedModel = identity.Model;
            }
            else if (!identity.IsTrusted)
                throw new InvalidOperationException("Blocked untrusted headset. Connect the trusted headset or explicitly trust this device.");
        }
        var quest = new AdbDevice { Serial = identity.AdbSerial, State = "device", Model = identity.Model };
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

    private static string CreateRecordingPath(DateTimeOffset capturedAt, string? model, string remotePath)
    {
        Directory.CreateDirectory(AppPaths.CapturesDirectory);
        var extension = Path.GetExtension(remotePath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp4";
        }

        var sourceName = Path.GetFileNameWithoutExtension(remotePath.Replace('\\', '/'));
        var sourceToken = SanitizeFileToken(sourceName);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var fileName = BuildRecordingFileName(capturedAt, model, sourceToken, extension, attempt == 0 ? null : attempt + 1);
            var path = Path.Combine(AppPaths.CapturesDirectory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(
            AppPaths.CapturesDirectory,
            BuildRecordingFileName(capturedAt, model, sourceToken, extension, duplicateIndex: null)
                .Replace(extension, "-" + Guid.NewGuid().ToString("N")[..8] + extension, StringComparison.OrdinalIgnoreCase));
    }

    internal static string BuildRecordingFileName(
        DateTimeOffset capturedAt,
        string? model,
        string? sourceName,
        string extension,
        int? duplicateIndex = null)
    {
        var timestamp = capturedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = duplicateIndex is null ? string.Empty : "-" + duplicateIndex.Value.ToString(CultureInfo.InvariantCulture);
        var source = SanitizeFileToken(sourceName);
        extension = string.IsNullOrWhiteSpace(extension) ? ".mp4" : extension.Trim();
        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        return $"QuestRecording-{timestamp}-{SanitizeFileToken(model)}-{source}{suffix}{extension}";
    }

    internal static IReadOnlyList<HeadsetRecordingCandidate> ParseRecordingListing(string listing)
    {
        var result = new List<HeadsetRecordingCandidate>();
        foreach (var raw in (listing ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('\t', 3);
            if (parts.Length != 3)
            {
                continue;
            }

            if (!long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var modified))
            {
                modified = 0;
            }

            if (!long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            {
                bytes = 0;
            }

            var remotePath = parts[2].Trim();
            if (remotePath.Length == 0
                || !(remotePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                     || remotePath.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(new HeadsetRecordingCandidate(modified, bytes, remotePath));
        }

        return result;
    }

    private static string BuildRecordingListingCommand()
    {
        var dirs = string.Join(" ", RecordingDirectories.Select(QuoteShellSingle));
        return "for d in " + dirs + "; do "
               + "if [ -d \"$d\" ]; then "
               + "for f in \"$d\"/*.mp4 \"$d\"/*.MP4 \"$d\"/*.mov \"$d\"/*.MOV; do "
               + "[ -f \"$f\" ] || continue; "
               + "m=$(stat -c %Y \"$f\" 2>/dev/null || echo 0); "
               + "s=$(stat -c %s \"$f\" 2>/dev/null || echo 0); "
               + "printf '%s\\t%s\\t%s\\n' \"$m\" \"$s\" \"$f\"; "
               + "done; "
               + "fi; "
               + "done";
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

    private static string QuoteShellSingle(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static void TryDelete(string path)
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
            // best-effort cleanup
        }
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

    private static bool TryDefaultTextureSize(string? model, out int width, out int height)
    {
        var name = (model ?? "").Replace('_', ' ').Trim();
        (width, height) = name switch
        {
            var m when m.Contains("Quest Pro", StringComparison.OrdinalIgnoreCase) => (1440, 1584),
            var m when m.Contains("Quest 3", StringComparison.OrdinalIgnoreCase) => (1680, 1760),
            var m when m.Contains("Quest 2", StringComparison.OrdinalIgnoreCase) => (1440, 1584),
            var m when m.Equals("Quest", StringComparison.OrdinalIgnoreCase)
                || m.Equals("Oculus Quest", StringComparison.OrdinalIgnoreCase)
                || m.Equals("Meta Quest", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Quest 1", StringComparison.OrdinalIgnoreCase) => (1216, 1344),
            _ => (0, 0)
        };
        return width > 0;
    }

    private static void AddExperimentalBool(
        IDictionary<string, string> result,
        HeadsetExperimentalOverride value,
        string property,
        string? unsupportedMessage,
        bool supported)
    {
        if (!Enum.IsDefined(value)) throw new InvalidOperationException("Unknown experimental headset override.");
        if (value == HeadsetExperimentalOverride.AppDefault) return;
        if (!supported) throw new InvalidOperationException(unsupportedMessage ?? "This experimental headset override is not supported by the connected model.");
        result[property] = value == HeadsetExperimentalOverride.ForceOn ? "1" : "0";
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
