using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DrawingIcon = System.Drawing.Icon;

namespace WinBox.Host.Ui;

/// <summary>
/// Builds a multi-resolution WinBox tray icon (16/20/24/32/48) as a real .ico stream.
/// Mark is a 2×2 window of rounded panes (Microsoft-logo-inspired silhouette),
/// not a lettermark or search glyph — reads as a capability "box".
/// </summary>
public static class TrayIconFactory
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 48];

    // Four-pane palette: same grammar as the Microsoft company mark (2×2 window),
    // hues shifted cooler / softer so we are inspired, not a clone.
    private static readonly Color PaneTopLeft = Color.FromArgb(255, 0xE2, 0x5B, 0x4A);
    private static readonly Color PaneTopRight = Color.FromArgb(255, 0x6C, 0xB8, 0x2E);
    private static readonly Color PaneBottomLeft = Color.FromArgb(255, 0x2A, 0x9C, 0xD6);
    private static readonly Color PaneBottomRight = Color.FromArgb(255, 0xEF, 0xB1, 0x2D);

    public static DrawingIcon Create()
    {
        var resource = TryLoadEmbedded();
        if (resource is not null)
        {
            return resource;
        }

        return CreateGenerated();
    }

    public static DrawingIcon CreateGenerated()
    {
        using var stream = BuildIcoStream(Sizes);
        return new DrawingIcon(stream);
    }

    /// <summary>Writes a multi-size .ico to disk (used by asset generation / packaging).</summary>
    public static void WriteToFile(string path, int[]? sizes = null)
    {
        using var stream = BuildIcoStream(sizes ?? Sizes);
        using var file = File.Create(path);
        stream.CopyTo(file);
    }

    private static DrawingIcon? TryLoadEmbedded()
    {
        var asm = typeof(TrayIconFactory).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("winbox.ico", StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        var stream = asm.GetManifestResourceStream(name);
        return stream is null ? null : new DrawingIcon(stream);
    }

    private static MemoryStream BuildIcoStream(int[] sizes)
    {
        var images = new List<byte[]>(sizes.Length);
        foreach (var size in sizes)
        {
            using var bmp = RenderMark(size);
            images.Add(EncodeBmpIconImage(bmp));
        }

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true))
        {
            writer.Write((ushort)0); // reserved
            writer.Write((ushort)1); // icon
            writer.Write((ushort)images.Count);

            var offset = 6 + (16 * images.Count);
            for (var i = 0; i < images.Count; i++)
            {
                var size = sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0); // palette
                writer.Write((byte)0);
                writer.Write((ushort)1); // planes
                writer.Write((ushort)32); // bit count
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (var image in images)
            {
                writer.Write(image);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static Bitmap RenderMark(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Outer margin + cross gap: keep panes chunky at 16px tray size.
        var margin = Math.Max(1f, size * 0.08f);
        var gap = Math.Max(1.25f, size * 0.08f);
        var pane = (size - (margin * 2) - gap) / 2f;
        var radius = Math.Max(1.1f, size * 0.10f);

        FillPane(g, margin, margin, pane, pane, radius, PaneTopLeft);
        FillPane(g, margin + pane + gap, margin, pane, pane, radius, PaneTopRight);
        FillPane(g, margin, margin + pane + gap, pane, pane, radius, PaneBottomLeft);
        FillPane(g, margin + pane + gap, margin + pane + gap, pane, pane, radius, PaneBottomRight);

        return bmp;
    }

    private static void FillPane(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        float radius,
        Color color)
    {
        var r = Math.Min(radius, Math.Min(width, height) / 2f);
        using var path = CreateRoundedRect(x, y, width, height, r);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2f;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static byte[] EncodeBmpIconImage(Bitmap bmp)
    {
        var width = bmp.Width;
        var height = bmp.Height;
        var xorStride = width * 4;
        var andStride = ((width + 31) / 32) * 4;
        var headerSize = 40;
        var xorSize = xorStride * height;
        var andSize = andStride * height;
        var buffer = new byte[headerSize + xorSize + andSize];

        // BITMAPINFOHEADER
        Buffer.BlockCopy(BitConverter.GetBytes(40), 0, buffer, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(width), 0, buffer, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(height * 2), 0, buffer, 8, 4); // includes mask
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, buffer, 12, 2); // planes
        Buffer.BlockCopy(BitConverter.GetBytes((short)32), 0, buffer, 14, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(0), 0, buffer, 16, 4); // BI_RGB
        Buffer.BlockCopy(BitConverter.GetBytes(xorSize), 0, buffer, 20, 4);

        var data = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            // BMP is bottom-up.
            for (var y = 0; y < height; y++)
            {
                var srcY = height - 1 - y;
                Marshal.Copy(
                    data.Scan0 + srcY * data.Stride,
                    buffer,
                    headerSize + y * xorStride,
                    xorStride);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        // AND mask left zeroed (alpha channel in XOR carries transparency).
        return buffer;
    }
}
