using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace WinBox.Host.Ui;

/// <summary>Shared WPF window icon sourced from the tray mark (avoids default .NET glyph).</summary>
internal static class WindowIconFactory
{
    private static ImageSource? _cached;

    public static ImageSource Create()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        using var icon = TrayIconFactory.Create();
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(32, 32));
        source.Freeze();
        _cached = source;
        return source;
    }

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Icon = Create();
    }
}
