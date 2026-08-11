using System.Windows.Media;
using MetaQuestTrayTool.Models;
using Color = System.Windows.Media.Color;

namespace MetaQuestTrayTool.Services;

public static class ThemeService
{
    public static event EventHandler<AppTheme>? Changed;
    public static AppTheme Current { get; private set; } = AppTheme.Black;

    public static void Apply(AppTheme theme)
    {
        Current = theme;
        var palette = Palette(theme);
        Set("AppBackgroundBrush", palette.Background);
        Set("AppSurfaceBrush", palette.Surface);
        Set("AppSidebarBrush", palette.Sidebar);
        Set("AppBorderBrush", palette.Border);
        Set("AppTextBrush", palette.Text);
        Set("AppMutedBrush", palette.Muted);
        Set("AppAccentBrush", palette.Accent);
        Set("AppAccentDarkBrush", palette.AccentDark);
        Set("AppAccentHoverBrush", palette.AccentHover);
        Set("AppFieldBrush", palette.Field);
        Set("AppHoverBrush", palette.Hover);
        Set("AppNavSelectedBrush", palette.NavSelected);
        Set("AppGhostButtonBrush", palette.Ghost);
        Set("AppGhostButtonHoverBrush", palette.GhostHover);
        Changed?.Invoke(null, theme);
    }

    public static TrayMenuPalette MenuPalette(AppTheme? theme = null)
    {
        var colors = Palette(theme ?? Current);
        return new TrayMenuPalette(
            Background: ToDrawing(colors.Background),
            Surface: ToDrawing(colors.Surface),
            Text: ToDrawing(colors.Text),
            Muted: ToDrawing(colors.Muted),
            Border: ToDrawing(colors.Border),
            Hover: ToDrawing(colors.Hover),
            Accent: ToDrawing(colors.Accent),
            AccentDark: ToDrawing(colors.AccentDark));
    }

    private static System.Drawing.Color ToDrawing(Color color) =>
        System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    private static void Set(string key, Color color)
    {
        var resources = System.Windows.Application.Current.Resources;
        if (resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static ThemePalette Palette(AppTheme theme) => theme switch
    {
        AppTheme.Light => new ThemePalette(
            Background: Color.FromRgb(0xF3, 0xF5, 0xF8),
            Surface: Color.FromRgb(0xFF, 0xFF, 0xFF),
            Sidebar: Color.FromRgb(0xE7, 0xEC, 0xF3),
            Border: Color.FromRgb(0xC5, 0xCD, 0xD8),
            Text: Color.FromRgb(0x1B, 0x24, 0x33),
            Muted: Color.FromRgb(0x3D, 0x4A, 0x5C),
            Accent: Color.FromRgb(0x0A, 0x6F, 0x7A),
            AccentDark: Color.FromRgb(0x0D, 0x7A, 0x7A),
            AccentHover: Color.FromRgb(0x0A, 0x68, 0x68),
            Field: Color.FromRgb(0xFF, 0xFF, 0xFF),
            Hover: Color.FromRgb(0xD9, 0xE1, 0xEC),
            NavSelected: Color.FromRgb(0xCC, 0xD8, 0xE6),
            Ghost: Color.FromRgb(0xD0, 0xD8, 0xE4),
            GhostHover: Color.FromRgb(0xBE, 0xC8, 0xD6)),
        AppTheme.Dark => new ThemePalette(
            Background: Color.FromRgb(0x0B, 0x12, 0x20),
            Surface: Color.FromRgb(0x15, 0x1D, 0x2E),
            Sidebar: Color.FromRgb(0x10, 0x18, 0x26),
            Border: Color.FromRgb(0x24, 0x30, 0x49),
            Text: Color.FromRgb(0xE8, 0xEE, 0xF8),
            Muted: Color.FromRgb(0xA8, 0xB4, 0xC8),
            Accent: Color.FromRgb(0x3D, 0xDC, 0xFF),
            AccentDark: Color.FromRgb(0x1A, 0xA6, 0xA6),
            AccentHover: Color.FromRgb(0x14, 0x88, 0x88),
            Field: Color.FromRgb(0x0B, 0x12, 0x20),
            Hover: Color.FromRgb(0x1C, 0x27, 0x3C),
            NavSelected: Color.FromRgb(0x24, 0x30, 0x49),
            Ghost: Color.FromRgb(0x24, 0x30, 0x49),
            GhostHover: Color.FromRgb(0x31, 0x40, 0x5F)),
        _ => new ThemePalette(
            Background: Color.FromRgb(0x00, 0x00, 0x00),
            Surface: Color.FromRgb(0x0A, 0x0A, 0x0A),
            Sidebar: Color.FromRgb(0x05, 0x05, 0x05),
            Border: Color.FromRgb(0x2A, 0x2A, 0x2A),
            Text: Color.FromRgb(0xF5, 0xF5, 0xF5),
            Muted: Color.FromRgb(0xB0, 0xB0, 0xB0),
            Accent: Color.FromRgb(0x3D, 0xDC, 0xFF),
            AccentDark: Color.FromRgb(0x1A, 0xA6, 0xA6),
            AccentHover: Color.FromRgb(0x14, 0x88, 0x88),
            Field: Color.FromRgb(0x11, 0x11, 0x11),
            Hover: Color.FromRgb(0x1A, 0x1A, 0x1A),
            NavSelected: Color.FromRgb(0x22, 0x22, 0x22),
            Ghost: Color.FromRgb(0x1C, 0x1C, 0x1C),
            GhostHover: Color.FromRgb(0x2C, 0x2C, 0x2C))
    };

    private readonly record struct ThemePalette(
        Color Background,
        Color Surface,
        Color Sidebar,
        Color Border,
        Color Text,
        Color Muted,
        Color Accent,
        Color AccentDark,
        Color AccentHover,
        Color Field,
        Color Hover,
        Color NavSelected,
        Color Ghost,
        Color GhostHover);
}
