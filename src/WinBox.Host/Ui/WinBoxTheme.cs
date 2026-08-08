using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Shared visual tokens for Host surfaces. Apply() swaps frozen brushes (cross-thread
/// safe) and raises <see cref="Changed"/> so open windows rebind Foreground/templates.
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
    public static SolidColorBrush HoverBrush { get; private set; } = null!;
    public static SolidColorBrush PrimaryButtonBrush { get; private set; } = null!;
    public static SolidColorBrush TextOnAccentBrush { get; private set; } = null!;
    public static SolidColorBrush TransparentBrush { get; } = Brushes.Transparent;

    public const double OverlayWidth = 600;
    public const double OverlayRadius = 14;
    public const double ControlRadius = 8;
    public const double SettingsCardRadius = 10;
    public const double SettingsPageMargin = 24;
    public const double SettingsContentPadding = 20;
    /// <summary>Right inset for settings tab body — keep thin so the page scrollbar sits near the edge.</summary>
    public const double SettingsContentPaddingRight = 4;
    public const double SettingsSectionGap = 18;
    public const double OverlayShadowPad = 18;
    public const double InputRowHeight = 56;
    public const double ResultsMaxHeight = 320;
    public const double ResultRowMinHeight = 44;
    public const double FontInput = 18;
    public const double FontTitle = 15;
    public const double FontSubtitle = 12;
    public const double FontFooter = 11;
    public const double FontGlyph = 14;
    public const double ResultIconSize = 16;
    public const int MotionMs = 100;

    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily GlyphFont = new("Segoe MDL2 Assets");

    public static UiThemeKind CurrentKind { get; private set; } = UiThemeKind.Dark;

    /// <summary>Effective dark/light after resolving System.</summary>
    public static bool IsDarkEffective { get; private set; } = true;

    public static event Action? Changed;

    static WinBoxTheme()
    {
        ApplyColors(DarkColors, isDark: true);
    }

    public static void Apply(UiThemeKind kind)
    {
        CurrentKind = kind;
        var colors = Resolve(kind);
        var effective = kind == UiThemeKind.System ? DetectSystemTheme() : kind;
        ApplyColors(colors, isDark: effective != UiThemeKind.Light);
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
        Abstractions.ResultActionKind.Submit => "\uE99A",
        _ => "\uE721",
    };

    /// <summary>
    /// Maps plugin <see cref="Abstractions.ResultIconKeys"/> to Segoe MDL2 glyphs.
    /// Unknown / empty keys fall back to the action glyph.
    /// </summary>
    public static string GlyphForResult(string? iconKey, Abstractions.ResultActionKind action)
    {
        if (!string.IsNullOrWhiteSpace(iconKey))
        {
            var glyph = GlyphForIconKey(iconKey);
            if (glyph is not null)
            {
                return glyph;
            }
        }

        return GlyphForAction(action);
    }

    public static string? GlyphForIconKey(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
        {
            return null;
        }

        return iconKey.Trim().ToLowerInvariant() switch
        {
            Abstractions.ResultIconKeys.Folder => "\uE8B7",
            Abstractions.ResultIconKeys.Document => "\uE8A5",
            Abstractions.ResultIconKeys.Markdown => "\uE70F",
            Abstractions.ResultIconKeys.Code => "\uE943",
            Abstractions.ResultIconKeys.Pdf => "\uE7C3",
            Abstractions.ResultIconKeys.Spreadsheet => "\uE9F9",
            Abstractions.ResultIconKeys.Presentation => "\uE7F4",
            Abstractions.ResultIconKeys.Image => "\uE8B9",
            Abstractions.ResultIconKeys.Audio => "\uE8D6",
            Abstractions.ResultIconKeys.Video => "\uE714",
            Abstractions.ResultIconKeys.Archive => "\uE88C",
            Abstractions.ResultIconKeys.Executable => "\uE756",
            Abstractions.ResultIconKeys.Calculator => "\uE8EF",
            Abstractions.ResultIconKeys.Shell => "\uE756",
            Abstractions.ResultIconKeys.Web => "\uE774",
            Abstractions.ResultIconKeys.Ai => "\uE99A",
            Abstractions.ResultIconKeys.Search => "\uE721",
            Abstractions.ResultIconKeys.File => "\uE8A5",
            _ => null,
        };
    }

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

    private static void ApplyColors(ThemeColors colors, bool isDark)
    {
        IsDarkEffective = isDark;
        SurfaceOverlayBrush = Freeze(colors.SurfaceOverlay);
        SurfaceRaisedBrush = Freeze(colors.SurfaceRaised);
        SurfaceSunkenBrush = Freeze(colors.SurfaceSunken);
        BorderSubtleBrush = Freeze(colors.BorderSubtle);
        TextPrimaryBrush = Freeze(colors.TextPrimary);
        TextSecondaryBrush = Freeze(colors.TextSecondary);
        AccentBrush = Freeze(colors.Accent);
        SelectionBrush = Freeze(colors.Selection);
        HoverBrush = Freeze(colors.Hover);
        PrimaryButtonBrush = Freeze(colors.PrimaryButton);
        TextOnAccentBrush = Freeze(colors.TextOnAccent);
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // Tuned for scanability + calm Fluent-ish hierarchy (not a third-party skin).
    private static readonly ThemeColors DarkColors = new(
        SurfaceOverlay: Color.FromRgb(0x1E, 0x1E, 0x1E),
        SurfaceRaised: Color.FromRgb(0x28, 0x28, 0x28),
        SurfaceSunken: Color.FromRgb(0x15, 0x15, 0x15),
        BorderSubtle: Color.FromRgb(0x3F, 0x3F, 0x46),
        TextPrimary: Color.FromRgb(0xF3, 0xF3, 0xF3),
        TextSecondary: Color.FromRgb(0xA0, 0xA0, 0xA8),
        Accent: Color.FromRgb(0x8A, 0xC7, 0xFF),
        Selection: Color.FromRgb(0x2A, 0x3F, 0x52),
        Hover: Color.FromRgb(0x2C, 0x2C, 0x30),
        PrimaryButton: Color.FromRgb(0x3A, 0x7C, 0xB5),
        TextOnAccent: Color.FromRgb(0xFF, 0xFF, 0xFF));

    private static readonly ThemeColors LightColors = new(
        SurfaceOverlay: Color.FromRgb(0xFF, 0xFF, 0xFF),
        SurfaceRaised: Color.FromRgb(0xF6, 0xF6, 0xF8),
        SurfaceSunken: Color.FromRgb(0xEE, 0xEE, 0xF1),
        BorderSubtle: Color.FromRgb(0xD8, 0xD8, 0xDE),
        TextPrimary: Color.FromRgb(0x1A, 0x1A, 0x1E),
        TextSecondary: Color.FromRgb(0x5A, 0x5A, 0x64),
        Accent: Color.FromRgb(0x00, 0x6C, 0xBF),
        Selection: Color.FromRgb(0xDC, 0xEC, 0xFB),
        Hover: Color.FromRgb(0xF0, 0xF0, 0xF3),
        PrimaryButton: Color.FromRgb(0x00, 0x6C, 0xBF),
        TextOnAccent: Color.FromRgb(0xFF, 0xFF, 0xFF));
}

public enum UiThemeKind
{
    Dark = 0,
    Light = 1,
    System = 2,
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
    Color Hover,
    Color PrimaryButton,
    Color TextOnAccent);
