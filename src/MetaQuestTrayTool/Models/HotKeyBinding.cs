namespace MetaQuestTrayTool.Models;

public sealed class HotKeyBinding
{
    public int Id { get; set; }
    public HotKeyAction Action { get; set; }
    public HotKeyModifiers Modifiers { get; set; }
    public string Key { get; set; } = "None";

    public HotKeyBinding Clone() => new()
    {
        Id = Id,
        Action = Action,
        Modifiers = Modifiers,
        Key = Key
    };

    public string DescribeAction() => HotKeyCatalog.DescribeAction(Action);

    public string DescribeChord()
    {
        if (!TryParseKey(out _))
        {
            return "(not set)";
        }

        var parts = new List<string>();
        if (Modifiers.HasFlag(HotKeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotKeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(HotKeyCatalog.DescribeKey(Key));
        return string.Join("+", parts);
    }

    public bool TryParseKey(out System.Windows.Input.Key key)
    {
        return Enum.TryParse(Key, ignoreCase: true, out key) && key != System.Windows.Input.Key.None;
    }
}
