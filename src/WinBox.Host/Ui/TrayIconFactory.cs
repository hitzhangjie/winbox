using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DrawingIcon = System.Drawing.Icon;

namespace WinBox.Host.Ui;

/// <summary>
/// Builds a multi-resolution WinBox tray icon (16/20/24/32/48) as a real .ico stream.
/// </summary>
public static class TrayIconFactory
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 48];

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

        // Circular badge reads cleaner at 16px than a rounded square.
        var pad = Math.Max(0.75f, size * 0.06f);
        var dia = size - (pad * 2);
        using (var tile = new SolidBrush(Color.FromArgb(255, 0x2A, 0x6F, 0xB5)))
        {
            g.FillEllipse(tile, pad, pad, dia, dia);
        }

        // Soft inner highlight for depth at larger sizes.
        if (size >= 24)
        {
            using var shine = new SolidBrush(Color.FromArgb(36, 255, 255, 255));
            g.FillEllipse(shine, pad + dia * 0.14f, pad + dia * 0.10f, dia * 0.55f, dia * 0.38f);
        }

        var stroke = Math.Max(1.25f, size * 0.10f);
        using var glass = new Pen(Color.FromArgb(255, 0xF7, 0xFA, 0xFF), stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        var lens = size * 0.38f;
        var lensLeft = size * 0.26f;
        var lensTop = size * 0.24f;
        g.DrawEllipse(glass, lensLeft, lensTop, lens, lens);

        var handleStartX = lensLeft + lens * 0.72f;
        var handleStartY = lensTop + lens * 0.72f;
        var handleEnd = size * 0.76f;
        g.DrawLine(glass, handleStartX, handleStartY, handleEnd, handleEnd);
        return bmp;
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
