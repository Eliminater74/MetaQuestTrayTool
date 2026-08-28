using System.Diagnostics;
using System.IO;
using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Pushes game settings through OculusDebugToolCLI.exe using a temporary command file.
/// Same mechanism Oculus Tray Tool and community batch files used.
/// </summary>
public sealed class OculusDebugToolService
{
    private readonly OculusRuntimeService _oculus;

    public OculusDebugToolService(OculusRuntimeService oculus)
    {
        _oculus = oculus;
    }

    public DebugToolApplyResult? LastResult { get; private set; }
    public GameSettings? LastApplied { get; private set; }
    public IReadOnlyList<string> LastHeadsetSerials { get; private set; } = [];
    public string? LastAswMode { get; private set; }

    public string? CliPath
    {
        get
        {
            _oculus.Refresh();
            return _oculus.DebugToolCliPath;
        }
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(CliPath) && File.Exists(CliPath);

    public IReadOnlyList<string> BuildCommands(GameSettings settings)
    {
        var lines = new List<string>();

        lines.Add($"service set-pixels-per-display-pixel-override {FormatNumber(settings.SuperSampling)}");

        switch (settings.AswMode)
        {
            case AswMode.Off:
                lines.Add("server:asw.Off");
                break;
            case AswMode.Auto:
                lines.Add("server:asw.Auto");
                break;
            case AswMode.Clock45:
                lines.Add("server:asw.Clock45");
                break;
            case AswMode.Clock30:
                lines.Add("server:asw.Clock30");
                break;
            case AswMode.Clock18:
                lines.Add("server:asw.Clock18");
                break;
        }

        var fovH = settings.FovMultiplierHorizontal;
        var fovV = settings.FovMultiplierVertical;
        if (Math.Abs(fovH - 1.0) > 0.001 || Math.Abs(fovV - 1.0) > 0.001)
        {
            lines.Add($"service set-client-fov-tan-angle-multiplier {FormatNumber(fovH)} {FormatNumber(fovV)}");
        }

        lines.Add($"service enable-adaptive-gpu-perf-scale {FormatBool(settings.AdaptiveGpuScaling)}");
        lines.Add($"service set-force-mip-gen-on-all-layers {FormatBool(settings.ForceMipMapOnLayers)}");
        lines.Add($"service set-offset-mip-bias-on-all-layers {FormatBool(Math.Abs(settings.OffsetMipMapOnLayers) > 0.001)}");
        lines.Add($"service set-use-fov-stencil {FormatBool(settings.UseFovStencil)}");

        if (settings.VisualHud == VisualHudMode.None)
        {
            lines.Add("perfhud reset");
        }
        else
        {
            lines.Add($"perfhud set-mode {(int)settings.VisualHud}");
        }

        lines.Add("exit");
        return lines;
    }

    public DebugToolApplyResult RunExtraCommands(IReadOnlyList<string> extra)
    {
        var commands = extra
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            .ToList();
        commands.Add("exit");
        return RunCommands(commands, timeoutMs: 15_000);
    }

    public IReadOnlyList<string> EnumerateHeadsets()
    {
        var result = RunCommands(["server:EnumHmd", "exit"], timeoutMs: 8_000);
        if (result is null || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        LastHeadsetSerials = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                line.Length > 0
                && !line.StartsWith("=>", StringComparison.Ordinal)
                && !line.StartsWith("Found these", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("no headset serial", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("Error:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return LastHeadsetSerials;
    }

    public string? QueryAswMode()
    {
        var result = RunCommands(["server:asw.Mode", "exit"], timeoutMs: 8_000);
        var line = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .LastOrDefault(item => !item.StartsWith("=>", StringComparison.Ordinal));
        LastAswMode = string.IsNullOrWhiteSpace(line) ? null : line;
        return LastAswMode;
    }

    public DebugToolApplyResult Apply(GameSettings settings, IEnumerable<string>? extraCli = null)
    {
        var commands = BuildCommands(settings).ToList();
        if (extraCli is not null)
        {
            var exit = commands.FindLastIndex(line => line.Equals("exit", StringComparison.OrdinalIgnoreCase));
            var extras = extraCli
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exit >= 0)
            {
                commands.InsertRange(exit, extras);
            }
            else
            {
                commands.AddRange(extras);
                commands.Add("exit");
            }
        }

        var result = RunCommands(commands, timeoutMs: 15_000);
        if (result.Succeeded)
        {
            LastApplied = settings.Clone();
            result.Summary = $"Applied {settings.Describe()}.";
        }
        else if (result.LooksRejected)
        {
            result.Summary = "Debug Tool ran, but Meta rejected one or more commands. Check the log — newer runtimes often block server: ASW commands.";
        }

        LastResult = result;
        return result;
    }

    private DebugToolApplyResult RunCommands(IReadOnlyList<string> commands, int timeoutMs)
    {
        _oculus.Refresh();
        var cli = _oculus.DebugToolCliPath;

        if (string.IsNullOrWhiteSpace(cli) || !File.Exists(cli))
        {
            return new DebugToolApplyResult
            {
                CliFound = false,
                Commands = commands,
                Summary = "OculusDebugToolCLI.exe was not found. Install the Meta Quest / Oculus PC software."
            };
        }

        var commandFile = System.IO.Path.Combine(
            AppPaths.AppDataDirectory,
            $"odt-commands-{Guid.NewGuid():N}.txt");
        AppPaths.EnsureAppDataDirectory();
        File.WriteAllLines(commandFile, commands);

        try
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cli,
                    Arguments = $"-f \"{commandFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(cli) ?? Environment.CurrentDirectory
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                return new DebugToolApplyResult
                {
                    CliFound = true,
                    Started = false,
                    Commands = commands,
                    Summary = "OculusDebugToolCLI.exe did not start."
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                return new DebugToolApplyResult
                {
                    CliFound = true,
                    Started = true,
                    TimedOut = true,
                    Commands = commands,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Summary = $"OculusDebugToolCLI.exe timed out after {timeoutMs / 1000} seconds."
                };
            }

            // Complete asynchronous output callbacks before evaluating the command result.
            process.WaitForExit();
            var combined = output + Environment.NewLine + error;
            return new DebugToolApplyResult
            {
                CliFound = true,
                Started = true,
                ExitCode = process.ExitCode,
                Commands = commands,
                Output = output.ToString().Trim(),
                Error = error.ToString().Trim(),
                LooksRejected = LooksRejected(combined),
                Summary = process.ExitCode == 0
                    ? "OculusDebugToolCLI finished."
                    : $"OculusDebugToolCLI failed with exit code {process.ExitCode}."
            };
        }
        catch (Exception ex)
        {
            return new DebugToolApplyResult
            {
                CliFound = true,
                Started = false,
                Commands = commands,
                Summary = $"Could not run OculusDebugToolCLI.exe: {ex.Message}"
            };
        }
        finally
        {
            try
            {
                File.Delete(commandFile);
            }
            catch
            {
                // leftover temp file
            }
        }
    }

    private static bool LooksRejected(string text)
    {
        return text.Contains("Unknown Command", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Server command failed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("failed with status", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatBool(bool value) => value ? "true" : "false";
}
