using System.Windows;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MetaQuestTrayTool.Models;

public enum StatusChipKind
{
    Off,
    On,
    Warn,
    Fail
}

/// <summary>One glowing status tile on the Status page.</summary>
public sealed class StatusChipVm
{
    public required string Title { get; init; }
    public required string Value { get; init; }
    public string? Detail { get; init; }
    public StatusChipKind Kind { get; init; }
    public string? ActionId { get; init; }
    public string? ActionLabel { get; init; }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionId) && !string.IsNullOrWhiteSpace(ActionLabel);

    public Visibility ActionVisibility => HasAction ? Visibility.Visible : Visibility.Collapsed;

    public string KindLabel => Kind switch
    {
        StatusChipKind.On => "ACTIVE",
        StatusChipKind.Warn => "CHECK",
        StatusChipKind.Fail => "OFF",
        _ => "IDLE"
    };

    public MediaBrush AccentBrush => Kind switch
    {
        StatusChipKind.On => new SolidColorBrush(MediaColor.FromRgb(0x3D, 0xDC, 0xFF)),
        StatusChipKind.Warn => new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xB0, 0x20)),
        StatusChipKind.Fail => new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x5C, 0x6C)),
        _ => new SolidColorBrush(MediaColor.FromRgb(0x55, 0x55, 0x55))
    };

    public MediaBrush TileBackground => Kind switch
    {
        StatusChipKind.On => new SolidColorBrush(MediaColor.FromRgb(0x08, 0x22, 0x2C)),
        StatusChipKind.Warn => new SolidColorBrush(MediaColor.FromRgb(0x2A, 0x1E, 0x08)),
        StatusChipKind.Fail => new SolidColorBrush(MediaColor.FromRgb(0x28, 0x10, 0x14)),
        _ => new SolidColorBrush(MediaColor.FromRgb(0x12, 0x12, 0x12))
    };

    public double TileOpacity => Kind == StatusChipKind.Off ? 0.55 : 1.0;
}
