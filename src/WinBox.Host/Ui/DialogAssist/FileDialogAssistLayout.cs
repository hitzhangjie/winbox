namespace WinBox.Host.Ui.DialogAssist;

/// <summary>Placement math for the file-dialog assist strip (fixed width, centered under dialog).</summary>
public static class FileDialogAssistLayout
{
    /// <summary>Listary-like fixed strip width in DIPs.</summary>
    public const double FixedWidthDip = 520;

    public const double MinWidthDip = 320;

    /// <summary>
    /// Places a fixed-width strip flush under the dialog, horizontally centered.
    /// If the dialog is narrower than the preferred width, the strip shrinks to fit.
    /// </summary>
    public static (double LeftDip, double TopDip, double WidthDip) PlaceUnderDialog(
        int dialogLeftPx,
        int dialogBottomPx,
        int dialogWidthPx,
        double dpiScale,
        double preferredWidthDip = FixedWidthDip)
    {
        if (dpiScale <= 0)
        {
            dpiScale = 1;
        }

        var dialogWidthDip = Math.Max(0, dialogWidthPx / dpiScale);
        var widthDip = Math.Clamp(preferredWidthDip, MinWidthDip, Math.Max(MinWidthDip, dialogWidthDip));
        if (dialogWidthDip > 0 && dialogWidthDip < MinWidthDip)
        {
            widthDip = dialogWidthDip;
        }

        var leftDip = dialogLeftPx / dpiScale + (dialogWidthDip - widthDip) / 2;
        var topDip = dialogBottomPx / dpiScale;
        return (leftDip, topDip, widthDip);
    }
}
