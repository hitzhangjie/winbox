using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WinBox.Host.Ui;

/// <summary>
/// Registers a process-wide hotkey against a WPF window HWND (works while the window is hidden).
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly int _hotkeyId;
    private readonly HwndSource _source;
    private readonly IntPtr _hwnd;
    private bool _disposed;

    public event Action? Pressed;

    public GlobalHotkey(Window window, ModifierKeys modifiers, Key key, int hotkeyId = 0x57B0)
    {
        ArgumentNullException.ThrowIfNull(window);

        _hotkeyId = hotkeyId;
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd)
            ?? throw new InvalidOperationException("Failed to obtain HwndSource for hotkey window.");

        _source.AddHook(WndProc);

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_hwnd, _hotkeyId, (uint)modifiers, (uint)vk))
        {
            throw new InvalidOperationException(
                $"Failed to register hotkey {modifiers}+{key}. It may already be in use.");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.RemoveHook(WndProc);
        NativeMethods.UnregisterHotKey(_hwnd, _hotkeyId);
        // Do not dispose _source — the Window owns the HWND / HwndSource lifetime.
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
