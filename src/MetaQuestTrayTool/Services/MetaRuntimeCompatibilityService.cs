using System.Diagnostics;
using System.IO;
using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public enum MetaCompatibilityLevel { Ok, Info, Warn }

public sealed record MetaCompatibilityFinding(
    MetaCompatibilityLevel Level,
    string Title,
    string Detail);

public sealed class MetaRuntimeCompatibilityReport
{
    public required DateTimeOffset CheckedAt { get; init; }
    public required IReadOnlyList<MetaCompatibilityFinding> Findings { get; init; }
    public bool HasWarnings => Findings.Any(item => item.Level == MetaCompatibilityLevel.Warn);
    public string Summary => HasWarnings
        ? $"Meta compatibility check found {Findings.Count(item => item.Level == MetaCompatibilityLevel.Warn)} warning(s)."
        : "Meta compatibility check found no local warnings.";

    public string ToDisplayText()
    {
        var text = new StringBuilder();
        text.AppendLine("Meta Runtime Compatibility Check");
        text.AppendLine($"Checked: {CheckedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine(Summary);
        text.AppendLine();
        foreach (var finding in Findings)
        {
            text.AppendLine($"[{finding.Level}] {finding.Title}");
            text.AppendLine($"  {finding.Detail}");
        }

        return text.ToString().TrimEnd();
    }
}

public sealed class MetaRuntimeCompatibilityService
{
    private readonly App _app;

    public MetaRuntimeCompatibilityService(App app) => _app = app;

    public MetaRuntimeCompatibilityReport Check(bool remember, bool runDebugToolProbe)
    {
        var findings = new List<MetaCompatibilityFinding>();
        _app.Oculus.Refresh(force: true);

        var runtimePath = ExistingPath(_app.Oculus.RuntimeServerPath) ?? ExistingPath(_app.Oculus.ResolveClientExePath());
        var runtimeVersion = ReadFileVersion(runtimePath);
        findings.Add(EvaluateObservedComponent(
            "Meta runtime",
            runtimeVersion,
            runtimePath,
            FirstNonEmpty(
                _app.Settings.Current.LastValidatedMetaRuntimeVersion,
                _app.Settings.Current.LastSeenMetaRuntimeVersion),
            FirstNonEmpty(
                _app.Settings.Current.LastValidatedMetaRuntimePath,
                _app.Settings.Current.LastSeenMetaRuntimePath),
            remember && runDebugToolProbe));

        var debugToolPath = ExistingPath(_app.Oculus.DebugToolCliPath) ?? ExistingPath(_app.Oculus.DebugToolGuiPath);
        var debugToolVersion = ReadFileVersion(debugToolPath);
        findings.Add(EvaluateObservedComponent(
            "Oculus Debug Tool",
            debugToolVersion,
            debugToolPath,
            FirstNonEmpty(
                _app.Settings.Current.LastValidatedOculusDebugToolVersion,
                _app.Settings.Current.LastSeenOculusDebugToolVersion),
            FirstNonEmpty(
                _app.Settings.Current.LastValidatedOculusDebugToolPath,
                _app.Settings.Current.LastSeenOculusDebugToolPath),
            remember && runDebugToolProbe));

        AddLinkRegistryFindings(findings);
        if (runDebugToolProbe)
        {
            AddDebugToolProbe(findings);
        }
        else
        {
            findings.Add(new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Info,
                "Oculus Debug Tool read probe skipped",
                "Startup records file versions only. Use Check Meta compatibility on the Info page to run the read-only CLI probe."));
        }

        findings.Add(new MetaCompatibilityFinding(
            MetaCompatibilityLevel.Info,
            "Read-only scope",
            "This checks local file versions, stored Link registry overrides, and optional OculusDebugToolCLI read probes. It does not prove that an active headset accepted runtime settings."));

        var report = new MetaRuntimeCompatibilityReport
        {
            CheckedAt = DateTimeOffset.Now,
            Findings = findings
        };

        if (remember)
        {
            Remember(runtimeVersion, runtimePath, debugToolVersion, debugToolPath, validateCurrent: runDebugToolProbe);
        }

        return report;
    }

    internal static MetaCompatibilityFinding EvaluateObservedComponent(
        string name,
        string? currentVersion,
        string? currentPath,
        string? lastVersion,
        string? lastPath) =>
        EvaluateObservedComponent(
            name,
            currentVersion,
            currentPath,
            lastVersion,
            lastPath,
            acknowledgeCurrentVersion: false);

    internal static MetaCompatibilityFinding EvaluateObservedComponent(
        string name,
        string? currentVersion,
        string? currentPath,
        string? lastVersion,
        string? lastPath,
        bool acknowledgeCurrentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(currentPath))
        {
            return new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Warn,
                name + " not found",
                "No readable executable version was found. Install or repair Meta Horizon Link before relying on ODT or Link settings.");
        }

        if (string.IsNullOrWhiteSpace(lastVersion))
        {
            return new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Info,
                name + " baseline recorded",
                $"{name} {currentVersion} at {currentPath}. Future checks will warn if this changes.");
        }

        if (!string.Equals(currentVersion, lastVersion, StringComparison.OrdinalIgnoreCase))
        {
            if (acknowledgeCurrentVersion)
            {
                return new MetaCompatibilityFinding(
                    MetaCompatibilityLevel.Info,
                    name + " validated after version change",
                    $"{name} is {currentVersion}; previously validated was {lastVersion}. This check saved the current version as the validated baseline.");
            }

            return new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Warn,
                name + " changed since last validated check",
                $"{name} is {currentVersion}; validated baseline is {lastVersion}. Re-test with Check Meta compatibility, or acknowledge the new runtime after external validation, before treating older mapping results as current.");
        }

        if (!string.IsNullOrWhiteSpace(lastPath)
            && !string.Equals(currentPath, lastPath, StringComparison.OrdinalIgnoreCase))
        {
            if (acknowledgeCurrentVersion)
            {
                return new MetaCompatibilityFinding(
                    MetaCompatibilityLevel.Info,
                    name + " path validated",
                    $"{name} is still {currentVersion}, and this check saved the current executable path as the validated baseline.");
            }

            return new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Info,
                name + " path changed",
                $"{name} is still {currentVersion}, but the executable path changed from {lastPath} to {currentPath}.");
        }

        return new MetaCompatibilityFinding(
            MetaCompatibilityLevel.Ok,
            name + " unchanged",
            $"{name} is still {currentVersion} at {currentPath}.");
    }

    private void AddLinkRegistryFindings(List<MetaCompatibilityFinding> findings)
    {
        try
        {
            var link = _app.Link.ReadCurrent();
            findings.Add(new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Ok,
                "Link registry readable",
                "Stored HKCU Link overrides: " + link.Describe() + ". This is registry state, not an active-headset acceptance check."));

            findings.Add(link.MobileAsw == MobileAswMode.Default
                ? new MetaCompatibilityFinding(
                    MetaCompatibilityLevel.Info,
                    "Mobile ASW default",
                    "No explicit Mobile ASW registry override is stored.")
                : new MetaCompatibilityFinding(
                    MetaCompatibilityLevel.Warn,
                    "Mobile ASW override unverified",
                    $"Mobile ASW is stored as {link.MobileAsw}. The registry value is readable, but current runtime acceptance remains unverified."));
        }
        catch (Exception ex)
        {
            findings.Add(new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Warn,
                "Link registry unreadable",
                "Could not read HKCU Link overrides: " + ex.Message));
        }
    }

    private void AddDebugToolProbe(List<MetaCompatibilityFinding> findings)
    {
        var result = _app.DebugTool.RunExtraCommands(["server:asw.Mode"]);
        if (result.Succeeded)
        {
            var output = string.IsNullOrWhiteSpace(result.Output) ? result.Summary : result.Output;
            findings.Add(new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Ok,
                "Oculus Debug Tool read probe succeeded",
                "server:asw.Mode returned: " + OneLine(output)));
            return;
        }

        var detail = string.IsNullOrWhiteSpace(result.Output + result.Error)
            ? result.Summary
            : OneLine((result.Output + " " + result.Error).Trim());
        findings.Add(new MetaCompatibilityFinding(
            result.CliFound ? MetaCompatibilityLevel.Warn : MetaCompatibilityLevel.Info,
            result.CliFound ? "Oculus Debug Tool read probe did not succeed" : "Oculus Debug Tool CLI not found",
            detail));
    }

    public MetaRuntimeCompatibilityReport AcknowledgeCurrentVersions()
    {
        var report = Check(remember: false, runDebugToolProbe: false);
        var runtimePath = ExistingPath(_app.Oculus.RuntimeServerPath) ?? ExistingPath(_app.Oculus.ResolveClientExePath());
        var runtimeVersion = ReadFileVersion(runtimePath);
        var debugToolPath = ExistingPath(_app.Oculus.DebugToolCliPath) ?? ExistingPath(_app.Oculus.DebugToolGuiPath);
        var debugToolVersion = ReadFileVersion(debugToolPath);

        Remember(runtimeVersion, runtimePath, debugToolVersion, debugToolPath, validateCurrent: true);
        return report;
    }

    private void Remember(
        string? runtimeVersion,
        string? runtimePath,
        string? debugToolVersion,
        string? debugToolPath,
        bool validateCurrent)
    {
        var runtime = BuildRememberedComponent(
            runtimeVersion,
            runtimePath,
            _app.Settings.Current.LastDetectedMetaRuntimeVersion,
            _app.Settings.Current.LastDetectedMetaRuntimePath,
            _app.Settings.Current.LastValidatedMetaRuntimeVersion,
            _app.Settings.Current.LastValidatedMetaRuntimePath,
            _app.Settings.Current.LastSeenMetaRuntimeVersion,
            _app.Settings.Current.LastSeenMetaRuntimePath,
            validateCurrent);
        _app.Settings.Current.LastDetectedMetaRuntimeVersion = runtime.DetectedVersion;
        _app.Settings.Current.LastDetectedMetaRuntimePath = runtime.DetectedPath;
        _app.Settings.Current.LastValidatedMetaRuntimeVersion = runtime.ValidatedVersion;
        _app.Settings.Current.LastValidatedMetaRuntimePath = runtime.ValidatedPath;
        _app.Settings.Current.LastSeenMetaRuntimeVersion = runtime.LegacySeenVersion;
        _app.Settings.Current.LastSeenMetaRuntimePath = runtime.LegacySeenPath;

        var debugTool = BuildRememberedComponent(
            debugToolVersion,
            debugToolPath,
            _app.Settings.Current.LastDetectedOculusDebugToolVersion,
            _app.Settings.Current.LastDetectedOculusDebugToolPath,
            _app.Settings.Current.LastValidatedOculusDebugToolVersion,
            _app.Settings.Current.LastValidatedOculusDebugToolPath,
            _app.Settings.Current.LastSeenOculusDebugToolVersion,
            _app.Settings.Current.LastSeenOculusDebugToolPath,
            validateCurrent);
        _app.Settings.Current.LastDetectedOculusDebugToolVersion = debugTool.DetectedVersion;
        _app.Settings.Current.LastDetectedOculusDebugToolPath = debugTool.DetectedPath;
        _app.Settings.Current.LastValidatedOculusDebugToolVersion = debugTool.ValidatedVersion;
        _app.Settings.Current.LastValidatedOculusDebugToolPath = debugTool.ValidatedPath;
        _app.Settings.Current.LastSeenOculusDebugToolVersion = debugTool.LegacySeenVersion;
        _app.Settings.Current.LastSeenOculusDebugToolPath = debugTool.LegacySeenPath;

        _app.Settings.Save();
    }

    internal static MetaVersionMemoryUpdate BuildRememberedComponent(
        string? currentVersion,
        string? currentPath,
        string? detectedVersion,
        string? detectedPath,
        string? validatedVersion,
        string? validatedPath,
        string? legacySeenVersion,
        string? legacySeenPath,
        bool validateCurrent)
    {
        var observedVersion = FirstNonEmpty(detectedVersion);
        var observedPath = FirstNonEmpty(detectedPath);
        var effectiveValidatedVersion = FirstNonEmpty(validatedVersion, legacySeenVersion);
        var effectiveValidatedPath = FirstNonEmpty(validatedPath, legacySeenPath);
        var effectiveLegacyVersion = FirstNonEmpty(legacySeenVersion, effectiveValidatedVersion);
        var effectiveLegacyPath = FirstNonEmpty(legacySeenPath, effectiveValidatedPath);

        if (!string.IsNullOrWhiteSpace(currentVersion) && !string.IsNullOrWhiteSpace(currentPath))
        {
            observedVersion = currentVersion;
            observedPath = currentPath;
            if (validateCurrent || string.IsNullOrWhiteSpace(effectiveValidatedVersion))
            {
                effectiveValidatedVersion = currentVersion;
                effectiveValidatedPath = currentPath;
                effectiveLegacyVersion = currentVersion;
                effectiveLegacyPath = currentPath;
            }
        }

        return new MetaVersionMemoryUpdate(
            observedVersion,
            observedPath,
            effectiveValidatedVersion,
            effectiveValidatedPath,
            effectiveLegacyVersion,
            effectiveLegacyPath);
    }

    private static string? ReadFileVersion(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return FirstNonEmpty(info.ProductVersion, info.FileVersion);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExistingPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string OneLine(string value)
    {
        var line = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return line.Length <= 240 ? line : line[..240] + "...";
    }
}

public readonly record struct MetaVersionMemoryUpdate(
    string? DetectedVersion,
    string? DetectedPath,
    string? ValidatedVersion,
    string? ValidatedPath,
    string? LegacySeenVersion,
    string? LegacySeenPath);
