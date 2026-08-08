using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Pure helpers for AI overlay fit: widen for readable lines (capped), and stream scroll pinning.
/// </summary>
public static class LauncherOverlayFit
{
    /// <summary>Temporary AI widen may not exceed this × base overlay width.</summary>
    public const double MaxWidthFactor = 1.5;

    /// <summary>Pixels from the bottom that still count as “following” the stream.</summary>
    public const double NearBottomTolerancePx = 28;

    /// <summary>
    /// Extra chrome outside the text: icon column, list/item padding, scrollbar gutter.
    /// </summary>
    public const double ContentChromePadding = 88;

    public static double ClampContentWidth(double baseWidth, double desiredContentWidth)
    {
        if (baseWidth <= 0)
        {
            baseWidth = WinBoxTheme.OverlayWidth;
        }

        var max = baseWidth * MaxWidthFactor;
        var desired = double.IsNaN(desiredContentWidth) || desiredContentWidth <= 0
            ? baseWidth
            : desiredContentWidth;
        return Math.Clamp(desired, baseWidth, max);
    }

    public static bool IsNearBottom(
        double verticalOffset,
        double viewportHeight,
        double extentHeight,
        double tolerancePx = NearBottomTolerancePx)
    {
        var scrollable = Math.Max(0, extentHeight - viewportHeight);
        return scrollable <= tolerancePx || verticalOffset >= scrollable - tolerancePx;
    }

    /// <summary>
    /// Auto-follow only while streaming multiline output and the user has not scrolled away.
    /// </summary>
    public static bool ShouldFollowStream(
        bool followPinned,
        bool isStreamingMultiline) =>
        followPinned && isStreamingMultiline;

    /// <summary>
    /// Estimate preferred content width from the longest line (no wrap), plus chrome padding.
    /// </summary>
    public static double EstimatePreferredContentWidth(
        string? text,
        double fontSize,
        FontFamily fontFamily,
        double pixelsPerDip = 1.0,
        double chromePadding = ContentChromePadding)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        pixelsPerDip = pixelsPerDip <= 0 ? 1.0 : pixelsPerDip;
        fontSize = fontSize <= 0 ? WinBoxTheme.FontTitle : fontSize;
        fontFamily ??= WinBoxTheme.UiFont;

        var typeface = new Typeface(
            fontFamily,
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);

        double maxLine = 0;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var line in normalized.Split('\n'))
        {
            var sample = line.Length == 0 ? " " : line;
            var formatted = new FormattedText(
                sample,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                pixelsPerDip);
            maxLine = Math.Max(maxLine, formatted.WidthIncludingTrailingWhitespace);
        }

        return maxLine + Math.Max(0, chromePadding);
    }
}
