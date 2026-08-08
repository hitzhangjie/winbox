using System.Windows;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Shared visual tokens for Host surfaces (overlay, settings, tray accents).
/// Keep magic RGB out of individual windows.
/// </summary>
public static class WinBoxTheme
{
    public static readonly Color SurfaceOverlay = Color.FromRgb(0x1C, 0x1C, 0x1C);
    public static readonly Color SurfaceRaised = Color.FromRgb(0x24, 0x24, 0x24);
    public static readonly Color SurfaceSunken = Color.FromRgb(0x14, 0x14, 0x14);
    public static readonly Color BorderSubtle = Color.FromRgb(0x3A, 0x3A, 0x3A);
    public static readonly Color TextPrimary = Color.FromRgb(0xF2, 0xF2, 0xF2);
    public static readonly Color TextSecondary = Color.FromRgb(0x9A, 0x9A, 0x9A);
    public static readonly Color Accent = Color.FromRgb(0x8A, 0xC7, 0xFF);
    public static readonly Color Selection = Color.FromRgb(0x2A, 0x3A, 0x48);
    public static readonly Color PrimaryButton = Color.FromRgb(0x2F, 0x6F, 0xAD);

    public static readonly SolidColorBrush SurfaceOverlayBrush = Freeze(SurfaceOverlay);
    public static readonly SolidColorBrush SurfaceRaisedBrush = Freeze(SurfaceRaised);
    public static readonly SolidColorBrush SurfaceSunkenBrush = Freeze(SurfaceSunken);
    public static readonly SolidColorBrush BorderSubtleBrush = Freeze(BorderSubtle);
    public static readonly SolidColorBrush TextPrimaryBrush = Freeze(TextPrimary);
    public static readonly SolidColorBrush TextSecondaryBrush = Freeze(TextSecondary);
    public static readonly SolidColorBrush AccentBrush = Freeze(Accent);
    public static readonly SolidColorBrush SelectionBrush = Freeze(Selection);
    public static readonly SolidColorBrush PrimaryButtonBrush = Freeze(PrimaryButton);
    public static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    public const double OverlayWidth = 600;
    public const double OverlayRadius = 14;
    public const double InputRowHeight = 56;
    public const double ResultsMaxHeight = 320;
    public const double ResultRowMinHeight = 44;
    public const double FontInput = 18;
    public const double FontTitle = 14;
    public const double FontSubtitle = 12;
    public const double FontFooter = 11;
    public const double FontGlyph = 14;

    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily GlyphFont = new("Segoe MDL2 Assets");

    public static string GlyphForAction(Abstractions.ResultActionKind action) => action switch
    {
        Abstractions.ResultActionKind.OpenUrl => "\uE774",
        Abstractions.ResultActionKind.RunCommand => "\uE756",
        Abstractions.ResultActionKind.CopyText => "\uE8C8",
        Abstractions.ResultActionKind.OpenContainingFolder => "\uE8B7",
        Abstractions.ResultActionKind.OpenPath => "\uE8A5",
        _ => "\uE721",
    };

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
