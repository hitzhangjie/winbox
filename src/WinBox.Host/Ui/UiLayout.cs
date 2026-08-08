namespace WinBox.Host.Ui;

/// <summary>
/// Live layout knobs derived from <see cref="UiOptions"/>. Overlay / rows read these
/// instead of hard-coded theme constants for sizes.
/// </summary>
public static class UiLayout
{
    public static double OverlayWidth { get; private set; } = WinBoxTheme.OverlayWidth;

    public static double ResultsMaxHeight { get; private set; } = WinBoxTheme.ResultsMaxHeight;

    public static double FontInput { get; private set; } = WinBoxTheme.FontInput;

    public static double FontTitle { get; private set; } = WinBoxTheme.FontTitle;

    public static double FontSubtitle { get; private set; } = WinBoxTheme.FontSubtitle;

    public static double FontFooter { get; private set; } = WinBoxTheme.FontFooter;

    public static double ScrollBarWidth { get; private set; } = 8;

    public static ScrollBarShowMode ScrollBarMode { get; private set; } = ScrollBarShowMode.Auto;

    public static event Action? Changed;

    public static void Apply(UiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var normalized = UiOptionsStore.Normalize(options);
        OverlayWidth = normalized.OverlayWidth;
        ResultsMaxHeight = normalized.ResultsMaxHeight;
        FontInput = normalized.FontInput;
        FontTitle = normalized.FontTitle;
        FontSubtitle = normalized.FontSubtitle;
        FontFooter = Math.Max(10, normalized.FontSubtitle - 1);
        ScrollBarWidth = normalized.ScrollBarWidth;
        ScrollBarMode = ParseScrollBarMode(normalized.ScrollBarMode);
        Changed?.Invoke();
    }

    public static ScrollBarShowMode ParseScrollBarMode(string? value)
    {
        if (string.Equals(value, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            return ScrollBarShowMode.Hidden;
        }

        if (string.Equals(value, "always", StringComparison.OrdinalIgnoreCase))
        {
            return ScrollBarShowMode.Always;
        }

        return ScrollBarShowMode.Auto;
    }

    public static string ToStorage(ScrollBarShowMode mode) => mode switch
    {
        ScrollBarShowMode.Hidden => "hidden",
        ScrollBarShowMode.Always => "always",
        _ => "auto",
    };

    public static System.Windows.Controls.ScrollBarVisibility ToVisibility(ScrollBarShowMode mode) =>
        mode switch
        {
            ScrollBarShowMode.Hidden => System.Windows.Controls.ScrollBarVisibility.Hidden,
            ScrollBarShowMode.Always => System.Windows.Controls.ScrollBarVisibility.Visible,
            _ => System.Windows.Controls.ScrollBarVisibility.Auto,
        };
}
