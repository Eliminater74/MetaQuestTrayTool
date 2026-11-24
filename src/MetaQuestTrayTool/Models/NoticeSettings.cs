namespace MetaQuestTrayTool.Models;

public sealed class NoticeSettings
{
    public bool ModernFlyouts { get; set; } = true;
    public bool Transparency { get; set; } = true;
    public bool Animations { get; set; } = true;
    public int DurationSeconds { get; set; } = 6;
    public bool PlaySound { get; set; }
    public bool KeepHistory { get; set; } = true;
    public bool Startup { get; set; } = true;
    public bool Headset { get; set; } = true;
    public bool Profile { get; set; } = true;
    public bool Performance { get; set; } = true;
    public bool Benchmark { get; set; } = true;
    public bool Update { get; set; } = true;
    public bool General { get; set; } = true;
    public bool LeftClickOpensQuickControls { get; set; }
}
