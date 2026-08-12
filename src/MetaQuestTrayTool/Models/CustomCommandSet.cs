namespace MetaQuestTrayTool.Models;

/// <summary>User-owned extra commands run after built-in CLI / ADB applies.</summary>
public sealed class CustomCommandSet
{
    public List<string> CliCommands { get; set; } = [];
    public List<string> AdbCommands { get; set; } = [];

    public bool HasAny => CliCommands.Count > 0 || AdbCommands.Count > 0;

    public string ToCliText() => string.Join(Environment.NewLine, CliCommands);
    public string ToAdbText() => string.Join(Environment.NewLine, AdbCommands);

    public void SetCliFromText(string? text) => CliCommands = ParseLines(text);
    public void SetAdbFromText(string? text) => AdbCommands = ParseLines(text);

    public static List<string> ParseLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }
}
