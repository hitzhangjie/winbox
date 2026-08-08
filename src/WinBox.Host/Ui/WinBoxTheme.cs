using System.Windows;
using System.Windows.Media;

namespace WinBox.Host.Ui;

public enum UiThemeKind
{
    Dark = 0,
    Light = 1,
    System = 2,
}

/// <summary>
/// Shared visual tokens for Host surfaces. Apply() swaps frozen brushes and raises
/// <see cref="Changed"/> so open windows can rebind.
/// </summary>
public static class WinBoxTheme
{
    public static SolidColorBrush SurfaceOverlayBrush { get; private set; } = null!;
    public static SolidColorBrush SurfaceRaisedBrush { get; private set; } = null!;
    public static SolidColorBrush SurfaceSunkenBrush { get; private set; } = null!;
    public static SolidColorBrush BorderSubtleBrush { get; private set; } = null!;
    public static SolidColorBrush TextPrimaryBrush { get; private set; } = null!;
    public static SolidColorBrush TextSecondaryBrush { get; private set; } = null!;
    public static SolidColorBrush AccentBrush { get; private set; } = null!;
    public static SolidColorBrush SelectionBrush { get; private set; } = null!;
    public static SolidColorBrush PrimaryButtonBrush { get; private set; } = null!;
    public static SolidColorBrush TransparentBrush { get; } = Brushes.Transparent;

    public const double OverlayWidth = 600;
    public const double OverlayRadius = 14;
    public const double OverlayShadowPad = 18;
    public const double InputRowHeight = 56;
    public const double ResultsMaxHeight = 320;
    public const double ResultRowMinHeight = 44;
    public const double FontInput = 18;
    public const double FontTitle = 14;
    public const double FontSubtitle = 12;
    public const double FontFooter = 11;
    public const double FontGlyph = 14;
    public const int MotionMs = 100;

    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily GlyphFont = new("Segoe MDL2 Assets");

    public static UiThemeKind CurrentKind { get; private set; } = UiThemeKind.Dark;

    public static event Action? Changed;

    static WinBoxTheme()
    {
        ApplyColors(DarkColors);
    }

    public static void Apply(UiThemeKind kind)
    {
        CurrentKind = kind;
        ApplyColors(Resolve(kind));
        Changed?.Invoke();
    }

    public static UiThemeKind ParseTheme(string? value)
    {
        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
        {
            return UiThemeKind.Light;
        }

        if (string.Equals(value, "system", StringComparison.OrdinalIgnoreCase))
        {
            return UiThemeKind.System;
        }

        return UiThemeKind.Dark;
    }

    public static string ToStorage(UiThemeKind kind) => kind switch
    {
        UiThemeKind.Light => "light",
        UiThemeKind.System => "system",
        _ => "dark",
    };

    public static string GlyphForAction(Abstractions.ResultActionKind action) => action switch
    {
        Abstractions.ResultActionKind.OpenUrl => "\uE774",
        Abstractions.ResultActionKind.RunCommand => "\uE756",
        Abstractions.ResultActionKind.CopyText => "\uE8C8",
        Abstractions.ResultActionKind.OpenContainingFolder => "\uE8B7",
        Abstractions.ResultActionKind.OpenPath => "\uE8A5",
        _ => "\uE721",
    };

    public static ThemeColors Resolve(UiThemeKind kind)
    {
        var effective = kind == UiThemeKind.System ? DetectSystemTheme() : kind;
        return effective == UiThemeKind.Light ? LightColors : DarkColors;
    }

    public static UiThemeKind DetectSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
            {
                return i == 0 ? UiThemeKind.Dark : UiThemeKind.Light;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException)
        {
            // fall through
        }

        return UiThemeKind.Dark;
    }

    private static void ApplyColors(ThemeColors colors)
    {
        SurfaceOverlayBrush = Freeze(colors.SurfaceOverlay);
        SurfaceRaisedBrush = Freeze(colors.SurfaceRaised);
        SurfaceSunkenBrush = Freeze(colors.SurfaceSunken);
        BorderSubtleBrush = Freeze(colors.BorderSubtle);
        TextPrimaryBrush = Freeze(colors.TextPrimary);
        TextSecondaryBrush = Freeze(colors.TextSecondary);
        AccentBrush = Freeze(colors.Accent);
        SelectionBrush = Freeze(colors.Selection);
        PrimaryButtonBrush = Freeze(colors.PrimaryButton);
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static readonly ThemeColors DarkColors = new(
        SurfaceOverlay: Color.FromRgb(0x1C, 0x1C, 0x1C),
        SurfaceRaised: Color.FromRgb(0x24, 0x24, 0x24),
        SurfaceSunken: Color.FromRgb(0x14, 0x14, 0x14),
        BorderSubtle: Color.FromRgb(0x3A, 0x3A, 0x3A),
        TextPrimary: Color.FromRgb(0xF2, 0xF2, 0xF2),
        TextSecondary: Color.FromRgb(0x9A, 0x9A, 0x9A),
        Accent: Color.FromRgb(0x8A, 0xC7, 0xFF),
        Selection: Color.FromRgb(0x2A, 0x3A, 0x48),
        PrimaryButton: Color.FromRgb(0x2F, 0x6F, 0xAD));

    private static readonly ThemeColors LightColors = new(
        SurfaceOverlay: Color.FromRgb(0xF7, 0xF7, 0xF8),
        SurfaceRaised: Color.FromRgb(0xFF, 0xFF, 0xFF),
        SurfaceSunken: Color.FromRgb(0xEE, 0xEE, 0xF0),
        BorderSubtle: Color.FromRgb(0xD0, 0xD0, 0xD4),
        TextPrimary: Color.FromRgb(0x1A, 0x1A, 0x1A),
        TextSecondary: Color.FromRgb(0x66, 0x66, 0x66),
        Accent: Color.FromRgb(0x00, 0x63, 0xB1),
        Selection: Color.FromRgb(0xD6, 0xE8, 0xF8),
        PrimaryButton: Color.FromRgb(0x00, 0x63, 0xB1));
}

public readonly record struct ThemeColors(
    Color SurfaceOverlay,
    Color SurfaceRaised,
    Color SurfaceSunken,
    Color BorderSubtle,
    Color TextPrimary,
    Color TextSecondary,
    Color Accent,
    Color Selection,
    Color PrimaryButton);
