using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DrawingFont = System.Drawing.Font;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace WinBox.Host.Ui;

/// <summary>
/// Notification-area icon with Open / Settings / Help / Quit. WinForms NotifyIcon hosted inside WPF.
/// </summary>
internal sealed class AppTrayIcon : IDisposable
{
    private const int MenuItemHeight = 40;
    private const int MenuItemMinWidth = 208;
    private const int MenuItemTextPadX = 52;

    private static readonly MethodInfo? ShowContextMenuMethod = typeof(Forms.NotifyIcon)
        .GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly Dispatcher _dispatcher;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly TrayMenuColorTable _menuColors;
    private readonly DrawingIcon _icon;
    private readonly DrawingFont _menuFont;
    private bool _disposed;

    public event Action? OpenLauncherRequested;
    public event Action? OpenSettingsRequested;
    public event Action? OpenHelpRequested;
    public event Action? ExitRequested;

    public AppTrayIcon(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _icon = TrayIconFactory.Create();
        _menuFont = new DrawingFont("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        _menuColors = new TrayMenuColorTable();

        _menu = new Forms.ContextMenuStrip
        {
            Font = _menuFont,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            AutoSize = true,
            Padding = new Forms.Padding(6, 8, 6, 8),
            DropShadowEnabled = true,
            Renderer = new TrayMenuRenderer(_menuColors),
        };
        _menu.Opening += (_, _) => TrayMenuNative.ApplyRoundedWindow(_menu);
        _menu.Items.Add(CreateItem("Open Launcher", RaiseOpen));
        _menu.Items.Add(CreateItem("Open Settings", RaiseSettings));
        _menu.Items.Add(CreateItem("Help", RaiseHelp));
        _menu.Items.Add(new Forms.ToolStripSeparator
        {
            Margin = new Forms.Padding(12, 6, 12, 6),
            AutoSize = false,
            Height = 9,
        });
        _menu.Items.Add(CreateItem("Quit WinBox !!", RaiseExit));
        FitMenuItemWidths();
        ApplyMenuTheme();

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
        WinBoxTheme.Changed += OnThemeChanged;
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

    private Forms.ToolStripMenuItem CreateItem(string text, EventHandler onClick)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            Height = MenuItemHeight,
            Padding = new Forms.Padding(18, 0, 22, 0),
            Margin = new Forms.Padding(2, 2, 2, 2),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };
        item.Click += onClick;
        return item;
    }

    private void FitMenuItemWidths()
    {
        var width = MenuItemMinWidth;
        foreach (Forms.ToolStripItem item in _menu.Items)
        {
            if (item is not Forms.ToolStripMenuItem menuItem || string.IsNullOrEmpty(menuItem.Text))
            {
                continue;
            }

            var textWidth = Forms.TextRenderer.MeasureText(
                menuItem.Text,
                _menuFont,
                System.Drawing.Size.Empty,
                Forms.TextFormatFlags.NoPrefix | Forms.TextFormatFlags.NoPadding).Width;
            width = Math.Max(width, textWidth + MenuItemTextPadX);
        }

        foreach (Forms.ToolStripItem item in _menu.Items)
        {
            if (item is Forms.ToolStripMenuItem menuItem)
            {
                menuItem.Width = width;
                menuItem.Height = MenuItemHeight;
            }
            else if (item is Forms.ToolStripSeparator)
            {
                item.Width = width;
            }
        }
    }

    private void RaiseOpen(object? sender, EventArgs e) => Raise(OpenLauncherRequested);

    private void RaiseSettings(object? sender, EventArgs e) => Raise(OpenSettingsRequested);

    private void RaiseHelp(object? sender, EventArgs e) => Raise(OpenHelpRequested);

    private void RaiseExit(object? sender, EventArgs e) => Raise(ExitRequested);

    private void OnThemeChanged()
    {
        if (_disposed)
        {
            return;
        }

        void Apply() => ApplyMenuTheme();
        if (_dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _dispatcher.BeginInvoke(Apply);
        }
    }

    private void ApplyMenuTheme()
    {
        _menuColors.RefreshFromTheme();
        _menu.BackColor = _menuColors.Surface;
        _menu.ForeColor = _menuColors.Text;
        _menu.Renderer = new TrayMenuRenderer(_menuColors);
        FitMenuItemWidths();
        foreach (Forms.ToolStripItem item in _menu.Items)
        {
            item.ForeColor = _menuColors.Text;
            item.BackColor = _menuColors.Surface;
            if (item is Forms.ToolStripMenuItem menuItem)
            {
                menuItem.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            }
        }
    }

    private void OnNotifyIconMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Right || _disposed)
        {
            return;
        }

        ApplyMenuTheme();
        TrayMenuNative.ApplyRoundedWindow(_menu);

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
        WinBoxTheme.Changed -= OnThemeChanged;
        _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _menuFont.Dispose();
        _icon.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
