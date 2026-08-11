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
        }

        if (Math.Abs(settings.FovMultiplier - 1.0) > 0.001)
        {
            var fov = FormatNumber(settings.FovMultiplier);
            lines.Add($"service set-client-fov-tan-angle-multiplier {fov} {fov}");
        }

        lines.Add("exit");
        return lines;
    }

    public DebugToolApplyResult Apply(GameSettings settings)
    {
        _oculus.Refresh();
        var cli = _oculus.DebugToolCliPath;

        if (string.IsNullOrWhiteSpace(cli) || !File.Exists(cli))
        {
            LastResult = new DebugToolApplyResult
            {
                CliFound = false,
                Summary = "OculusDebugToolCLI.exe was not found. Install the Meta Quest / Oculus PC software."
            };
            return LastResult;
        }

        var commands = BuildCommands(settings);
        var commandFile = System.IO.Path.Combine(AppPaths.AppDataDirectory, "odt-commands.txt");
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
                LastResult = new DebugToolApplyResult
                {
                    CliFound = true,
                    Started = false,
                    Commands = commands,
                    Summary = "OculusDebugToolCLI.exe did not start."
                };
                return LastResult;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(15_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                LastResult = new DebugToolApplyResult
                {
                    CliFound = true,
                    Started = true,
                    Commands = commands,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Summary = "OculusDebugToolCLI.exe timed out after 15 seconds."
                };
                return LastResult;
            }

            var combined = output + Environment.NewLine + error;
            var rejected = LooksRejected(combined);

            LastApplied = settings.Clone();
            LastResult = new DebugToolApplyResult
            {
                CliFound = true,
                Started = true,
                ExitCode = process.ExitCode,
                Commands = commands,
                Output = output.ToString().Trim(),
                Error = error.ToString().Trim(),
                LooksRejected = rejected,
                Summary = rejected
                    ? "Debug Tool ran, but Meta rejected one or more commands. Check the log — newer runtimes often block server: ASW commands."
                    : $"Applied {settings.Describe()}."
            };
            return LastResult;
        }
        catch (Exception ex)
        {
            LastResult = new DebugToolApplyResult
            {
                CliFound = true,
                Started = false,
                Commands = commands,
                Summary = $"Could not run OculusDebugToolCLI.exe: {ex.Message}"
            };
            return LastResult;
        }
    }

    private static bool LooksRejected(string text)
    {
        return text.Contains("Unknown Command", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Server command failed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("failed with status", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNumber(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
