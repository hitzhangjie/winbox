namespace WinBox.Host.Ui;

/// <summary>Persisted launcher chrome preferences (position / width).</summary>
public sealed class UiOptions
{
    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public double OverlayWidth { get; set; } = WinBoxTheme.OverlayWidth;
}
