using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinBoxThemeMedia = System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>Fluent-ish tray context menu colors matched to <see cref="WinBoxTheme"/>.</summary>
internal sealed class TrayMenuColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.Transparent;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Selection;
    public override Color MenuItemSelectedGradientBegin => Selection;
    public override Color MenuItemSelectedGradientEnd => Selection;
    public override Color MenuItemPressedGradientBegin => Selection;
    public override Color MenuItemPressedGradientEnd => Selection;
    public override Color MenuItemPressedGradientMiddle => Selection;
    public override Color ImageMarginGradientBegin => Surface;
    public override Color ImageMarginGradientMiddle => Surface;
    public override Color ImageMarginGradientEnd => Surface;
    public override Color ToolStripDropDownBackground => Surface;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;

    public Color Surface { get; private set; }
    public Color Border { get; private set; }
    public Color Selection { get; private set; }
    public Color Text { get; private set; }
    public Color Muted { get; private set; }

    public TrayMenuColorTable() => RefreshFromTheme();

    public void RefreshFromTheme()
    {
        Surface = ToDrawing(WinBoxTheme.SurfaceOverlayBrush.Color);
        Border = ToDrawing(WinBoxTheme.BorderSubtleBrush.Color);
        Selection = ToDrawing(WinBoxTheme.SelectionBrush.Color);
        Text = ToDrawing(WinBoxTheme.TextPrimaryBrush.Color);
        Muted = ToDrawing(WinBoxTheme.TextSecondaryBrush.Color);
    }

    private static Color ToDrawing(WinBoxThemeMedia.Color c) =>
        Color.FromArgb(c.A, c.R, c.G, c.B);
}

/// <summary>Rounded tray menu with vertically centered labels.</summary>
internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    private const int CornerRadius = 10;
    private const int ItemRadius = 7;
    private readonly TrayMenuColorTable _colors;

    public TrayMenuRenderer(TrayMenuColorTable colors)
        : base(colors)
    {
        _colors = colors;
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.Transparent);
        var bounds = Rectangle.Inflate(e.AffectedBounds, -1, -1);
        using var path = Rounded(bounds, CornerRadius);
        using (var brush = new SolidBrush(_colors.Surface))
        {
            e.Graphics.FillPath(brush, path);
        }

        using var pen = new Pen(_colors.Border);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Border is drawn in background pass.
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(8, 3, e.Item.Width - 16, e.Item.Height - 6);
        using var brush = new SolidBrush(_colors.Selection);
        using var path = Rounded(rect, ItemRadius);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var color = e.Item.Enabled ? _colors.Text : _colors.Muted;
        var left = Math.Max(e.TextRectangle.X, e.Item.ContentRectangle.X + 4);
        var rect = new Rectangle(
            left,
            0,
            Math.Max(8, e.Item.Width - left - 12),
            e.Item.Height);

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont,
            rect,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Bounds.Top + (e.Item.Bounds.Height / 2);
        using var pen = new Pen(_colors.Border);
        e.Graphics.DrawLine(pen, 14, y, e.Item.Width - 14, y);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(2, radius * 2);
        if (bounds.Width < d || bounds.Height < d)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class TrayMenuNative
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    public static void ApplyRoundedWindow(ToolStripDropDown menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (!menu.IsHandleCreated)
        {
            _ = menu.Handle;
        }

        var hwnd = menu.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var corner = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        // Soft region fallback when DWM rounding is unavailable.
        var bounds = new Rectangle(0, 0, menu.Width, menu.Height);
        using var path = new GraphicsPath();
        const int r = 10;
        var d = r * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(bounds.Width - d, 0, d, d, 270, 90);
        path.AddArc(bounds.Width - d, bounds.Height - d, d, d, 0, 90);
        path.AddArc(0, bounds.Height - d, d, d, 90, 90);
        path.CloseFigure();
        menu.Region = new Region(path);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
