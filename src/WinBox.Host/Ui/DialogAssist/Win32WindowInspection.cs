using System.Runtime.InteropServices;
using System.Text;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>Production <see cref="IWindowInspection"/> backed by user32.</summary>
internal sealed class Win32WindowInspection : IWindowInspection
{
    public nint GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public bool IsWindow(nint hwnd) => NativeMethods.IsWindow(hwnd);

    public bool IsWindowVisible(nint hwnd) => NativeMethods.IsWindowVisible(hwnd);

    public string GetClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        return NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    public string GetWindowText(nint hwnd)
    {
        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        return NativeMethods.GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    public bool TryGetWindowRect(nint hwnd, out int left, out int top, out int right, out int bottom)
    {
        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            left = rect.Left;
            top = rect.Top;
            right = rect.Right;
            bottom = rect.Bottom;
            return true;
        }

        left = top = right = bottom = 0;
        return false;
    }

    public IReadOnlyList<nint> GetChildWindows(nint parent, bool recursive)
    {
        // EnumChildWindows already walks the full descendant tree.
        var list = new List<nint>(64);
        NativeMethods.EnumChildWindows(
            parent,
            (hwnd, _) =>
            {
                list.Add(hwnd);
                return true;
            },
            0);

        if (!recursive && list.Count > 0)
        {
            // Direct children only: keep windows whose parent is exactly `parent`.
            var direct = new List<nint>(list.Count);
            foreach (var hwnd in list)
            {
                if (NativeMethods.GetParent(hwnd) == parent)
                {
                    direct.Add(hwnd);
                }
            }

            return direct;
        }

        return list;
    }

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll")]
        public static extern nint GetParent(nint hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
