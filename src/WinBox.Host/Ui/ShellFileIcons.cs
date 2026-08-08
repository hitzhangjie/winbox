using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IOPath = System.IO.Path;

namespace WinBox.Host.Ui;

/// <summary>
/// Windows shell association icons (Explorer-style) for launcher result rows.
/// Cached by extension for ordinary files; per-path for .exe / .lnk / .ico when present on disk.
/// </summary>
internal static class ShellFileIcons
{
    private const uint FileAttributeNormal = 0x80;
    private const uint FileAttributeDirectory = 0x10;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const int MaxCacheEntries = 256;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!IOPath.IsPathRooted(path))
            {
                return null;
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        var key = BuildCacheKey(path);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= MaxCacheEntries)
        {
            Cache.Clear();
        }

        var loaded = Load(path);
        Cache[key] = loaded;
        return loaded;
    }

    /// <summary>Test hook: drop cached bitmaps.</summary>
    internal static void ClearCache() => Cache.Clear();

    private static string BuildCacheKey(string path)
    {
        string extension;
        try
        {
            extension = IOPath.GetExtension(path);
        }
        catch (ArgumentException)
        {
            return "invalid";
        }

        if (IsPerFileIconExtension(extension))
        {
            return "path:" + path;
        }

        if (string.IsNullOrEmpty(extension))
        {
            return "ext:";
        }

        return "ext:" + extension.ToLowerInvariant();
    }

    private static bool IsPerFileIconExtension(string extension) =>
        extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);

    private static ImageSource? Load(string path)
    {
        var flags = ShgfiIcon | ShgfiSmallIcon;
        var attributes = FileAttributeNormal;
        var queryPath = path;

        try
        {
            if (System.IO.Directory.Exists(path))
            {
                attributes = FileAttributeDirectory;
                flags |= ShgfiUseFileAttributes;
            }
            else
            {
                var extension = IOPath.GetExtension(path);
                var useRealFile = IsPerFileIconExtension(extension) && System.IO.File.Exists(path);
                if (!useRealFile)
                {
                    // Association icon from extension even when the indexed path is stale/missing.
                    flags |= ShgfiUseFileAttributes;
                    queryPath = string.IsNullOrEmpty(extension) ? "file" : "file" + extension;
                }
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(
            queryPath,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            flags);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
