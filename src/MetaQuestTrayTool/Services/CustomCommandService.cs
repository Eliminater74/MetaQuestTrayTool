using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class CustomCommandService
{
    private readonly OculusDebugToolService _debugTool;
    private readonly AdbService _adb;

    public CustomCommandService(OculusDebugToolService debugTool, AdbService adb)
    {
        _debugTool = debugTool;
        _adb = adb;
    }

    public string ApplyCli(IReadOnlyList<string> commands)
    {
        if (commands.Count == 0)
        {
            return string.Empty;
        }

        var result = _debugTool.RunExtraCommands(commands);
        return string.IsNullOrWhiteSpace(result.Summary)
            ? $"Ran {commands.Count} custom CLI command(s)."
            : result.Summary;
    }

    public string ApplyAdb(IReadOnlyList<string> commands, string serial)
    {
        if (commands.Count == 0)
        {
            return string.Empty;
        }

        var ran = 0;
        foreach (var raw in commands)
        {
            var shell = NormalizeAdbShell(raw);
            if (shell is null)
            {
                continue;
            }

            _adb.Shell(serial, shell);
            ran++;
        }

        return ran == 0 ? string.Empty : $"Ran {ran} custom ADB command(s).";
    }

    private static string? NormalizeAdbShell(string line)
    {
        var text = line.Trim();
        if (text.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].Trim();
            if (text.StartsWith("-s ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                text = rest.Length >= 3 ? rest[2] : text;
            }

            if (text.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
            {
                text = text[6..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
