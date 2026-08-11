namespace MetaQuestTrayTool.Services;

public readonly record struct TrayMenuPalette(
    System.Drawing.Color Background,
    System.Drawing.Color Surface,
    System.Drawing.Color Text,
    System.Drawing.Color Muted,
    System.Drawing.Color Border,
    System.Drawing.Color Hover,
    System.Drawing.Color Accent,
    System.Drawing.Color AccentDark);
