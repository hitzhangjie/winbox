namespace WinBox.Host.Ui;

public enum SettingsTab
{
    General = 0,
    Index = 1,
    Appearance = 2,
    Shortcuts = 3,
}

/// <summary>When the launcher vertical scrollbar is shown.</summary>
public enum ScrollBarShowMode
{
    /// <summary>Only when content overflows (default).</summary>
    Auto = 0,

    /// <summary>Never show the bar; wheel / keys still scroll.</summary>
    Hidden = 1,

    /// <summary>Always reserve/show the bar while results are visible.</summary>
    Always = 2,
}

/// <summary>Persisted launcher chrome preferences.</summary>
public sealed class UiOptions
{
    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public double OverlayWidth { get; set; } = WinBoxTheme.OverlayWidth;

    public double ResultsMaxHeight { get; set; } = WinBoxTheme.ResultsMaxHeight;

    public double FontInput { get; set; } = WinBoxTheme.FontInput;

    public double FontTitle { get; set; } = WinBoxTheme.FontTitle;

    public double FontSubtitle { get; set; } = WinBoxTheme.FontSubtitle;

    /// <summary>Scrollbar thickness in device-independent pixels (4–16).</summary>
    public double ScrollBarWidth { get; set; } = 8;

    /// <summary>auto | hidden | always</summary>
    public string ScrollBarMode { get; set; } = "auto";

    /// <summary>dark | light | system</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Start WinBox when the user signs in to Windows (HKCU Run).</summary>
    public bool StartWithWindows { get; set; }
}
