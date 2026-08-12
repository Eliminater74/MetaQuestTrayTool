namespace MetaQuestTrayTool.Models;

public sealed class HotKeySettings
{
    public bool Enabled { get; set; }

    public List<HotKeyBinding> Bindings { get; set; } = CreateDefaultBindings();

    public HotKeySettings Clone() => new()
    {
        Enabled = Enabled,
        Bindings = Bindings.Select(binding => binding.Clone()).ToList()
    };

    public static List<HotKeyBinding> CreateDefaultBindings() =>
    [
        new HotKeyBinding { Id = 1, Action = HotKeyAction.AswOff, Modifiers = HotKeyModifiers.Control, Key = "NumPad1" },
        new HotKeyBinding { Id = 2, Action = HotKeyAction.AswAuto, Modifiers = HotKeyModifiers.Control, Key = "NumPad2" },
        new HotKeyBinding { Id = 3, Action = HotKeyAction.AswClock45, Modifiers = HotKeyModifiers.Control, Key = "NumPad3" },
        new HotKeyBinding { Id = 4, Action = HotKeyAction.AswCycle, Modifiers = HotKeyModifiers.Control, Key = "NumPad4" },
        new HotKeyBinding { Id = 5, Action = HotKeyAction.SuperSamplingCycle, Modifiers = HotKeyModifiers.Control, Key = "NumPad5" },
        new HotKeyBinding { Id = 6, Action = HotKeyAction.ApplyGlobal, Modifiers = HotKeyModifiers.Control, Key = "NumPad6" },
        new HotKeyBinding { Id = 7, Action = HotKeyAction.RestartOvrService, Modifiers = HotKeyModifiers.Control, Key = "NumPad7" },
        new HotKeyBinding { Id = 8, Action = HotKeyAction.TogglePerfHud, Modifiers = HotKeyModifiers.Control, Key = "NumPad8" }
    ];

    public void EnsureBindingIds()
    {
        var nextId = Bindings.Count == 0 ? 1 : Bindings.Max(binding => binding.Id) + 1;
        foreach (var binding in Bindings.Where(binding => binding.Id <= 0))
        {
            binding.Id = nextId++;
        }
    }
}
