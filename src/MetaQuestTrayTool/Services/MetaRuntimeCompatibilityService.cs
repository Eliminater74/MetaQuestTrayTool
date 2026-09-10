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
            _app.Settings.Current.LastSeenMetaRuntimeVersion,
            _app.Settings.Current.LastSeenMetaRuntimePath));

        var debugToolPath = ExistingPath(_app.Oculus.DebugToolCliPath) ?? ExistingPath(_app.Oculus.DebugToolGuiPath);
        var debugToolVersion = ReadFileVersion(debugToolPath);
        findings.Add(EvaluateObservedComponent(
            "Oculus Debug Tool",
            debugToolVersion,
            debugToolPath,
            _app.Settings.Current.LastSeenOculusDebugToolVersion,
            _app.Settings.Current.LastSeenOculusDebugToolPath));

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
            Remember(runtimeVersion, runtimePath, debugToolVersion, debugToolPath);
        }

        return report;
    }

    internal static MetaCompatibilityFinding EvaluateObservedComponent(
        string name,
        string? currentVersion,
        string? currentPath,
        string? lastVersion,
        string? lastPath)
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
            return new MetaCompatibilityFinding(
                MetaCompatibilityLevel.Warn,
                name + " changed since last check",
                $"{name} is {currentVersion}; last seen was {lastVersion}. Re-test ODT/Link behavior before treating older mapping results as current.");
        }

        if (!string.IsNullOrWhiteSpace(lastPath)
            && !string.Equals(currentPath, lastPath, StringComparison.OrdinalIgnoreCase))
        {
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

    private void Remember(string? runtimeVersion, string? runtimePath, string? debugToolVersion, string? debugToolPath)
    {
        if (!string.IsNullOrWhiteSpace(runtimeVersion) && !string.IsNullOrWhiteSpace(runtimePath))
        {
            _app.Settings.Current.LastSeenMetaRuntimeVersion = runtimeVersion;
            _app.Settings.Current.LastSeenMetaRuntimePath = runtimePath;
        }

        if (!string.IsNullOrWhiteSpace(debugToolVersion) && !string.IsNullOrWhiteSpace(debugToolPath))
        {
            _app.Settings.Current.LastSeenOculusDebugToolVersion = debugToolVersion;
            _app.Settings.Current.LastSeenOculusDebugToolPath = debugToolPath;
        }

        _app.Settings.Save();
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
