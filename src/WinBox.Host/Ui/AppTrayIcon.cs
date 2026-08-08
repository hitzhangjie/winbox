using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace WinBox.Host.Ui;

/// <summary>
/// Notification-area icon with Open / Settings / Quit. WinForms NotifyIcon hosted inside WPF.
/// </summary>
internal sealed class AppTrayIcon : IDisposable
{
    private static readonly MethodInfo? ShowContextMenuMethod = typeof(Forms.NotifyIcon)
        .GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly Dispatcher _dispatcher;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly DrawingIcon _icon;
    private bool _disposed;

    public event Action? OpenLauncherRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;

    public AppTrayIcon(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _icon = TrayIconFactory.Create();

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Open launcher", null, (_, _) => Raise(OpenLauncherRequested));
        _menu.Items.Add("Settings…", null, (_, _) => Raise(OpenSettingsRequested));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Quit WinBox", null, (_, _) => Raise(ExitRequested));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "WinBox",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        // WPF message pump often fails to auto-open ContextMenuStrip; show it explicitly.
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        _notifyIcon.DoubleClick += (_, _) => Raise(OpenLauncherRequested);
    }

    public void ShowBalloon(string title, string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void OnNotifyIconMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Right || _disposed)
        {
            return;
        }

        // Bring menu to foreground so clicks land on our process.
        _ = NativeMethods.SetForegroundWindow(_menu.Handle);

        if (ShowContextMenuMethod is not null)
        {
            ShowContextMenuMethod.Invoke(_notifyIcon, null);
            return;
        }

        _menu.Show(Forms.Control.MousePosition);
    }

    private void Raise(Action? handler)
    {
        if (handler is null || _disposed)
        {
            return;
        }

        // Invoke (not BeginInvoke) so settings opens before the tray message completes.
        if (_dispatcher.CheckAccess())
        {
            handler();
        }
        else
        {
            _dispatcher.Invoke(handler);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
