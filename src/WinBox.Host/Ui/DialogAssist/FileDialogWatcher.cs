using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>
/// Watches for standard Open/Save dialogs via <c>SetWinEventHook</c>.
/// While the user drags/resizes the dialog the assist strip stays hidden; it snaps
/// to the final position only after move/resize ends (no mid-drag lag chasing).
/// </summary>
internal sealed class FileDialogWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMoveSizeStart = 0x000A;
    private const uint EventSystemMoveSizeEnd = 0x000B;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectDestroy = 0x8001;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;

    private readonly FileDialogDetector _detector;
    private readonly IWindowInspection _windows;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _idleBoundsTimer;
    private NativeMethods.WinEventDelegate? _callback;
    private nint _hookForeground;
    private nint _hookShow;
    private nint _hookHide;
    private nint _hookMove;
    private FileDialogTarget? _current;
    private nint _assistHwnd;
    private bool _disposed;
    private bool _enabled = true;
    private bool _moving;

    public event Action<FileDialogTarget>? DialogOpened;

    public event Action? DialogClosed;

    public event Action<FileDialogTarget>? DialogMoved;

    /// <summary>Raised when the user starts dragging/resizing the dialog — assist should hide.</summary>
    public event Action? DialogMoveStarted;

    /// <summary>Raised when drag/resize ends — assist should show at the final bounds.</summary>
    public event Action<FileDialogTarget>? DialogMoveEnded;

    public FileDialogTarget? Current => _current;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                ClearCurrent();
            }
            else
            {
                EvaluateForeground();
            }
        }
    }

    /// <summary>Assist strip HWND — focus here must not dismiss the active dialog target.</summary>
    public void SetAssistHwnd(nint hwnd) => _assistHwnd = hwnd;

    public FileDialogWatcher(
        FileDialogDetector detector,
        IWindowInspection windows,
        Dispatcher dispatcher)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _idleBoundsTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _idleBoundsTimer.Tick += (_, _) => SyncCurrentBounds();

        _callback = OnWinEvent;
        var flags = WineventOutOfContext | WineventSkipOwnProcess;
        _hookForeground = NativeMethods.SetWinEventHook(
            EventSystemForeground, EventSystemForeground, 0, _callback, 0, 0, flags);
        _hookShow = NativeMethods.SetWinEventHook(
            EventObjectShow, EventObjectShow, 0, _callback, 0, 0, flags);
        _hookHide = NativeMethods.SetWinEventHook(
            EventObjectHide, EventObjectDestroy, 0, _callback, 0, 0, flags);
        _hookMove = NativeMethods.SetWinEventHook(
            EventSystemMoveSizeStart, EventSystemMoveSizeEnd, 0, _callback, 0, 0, flags);

        EvaluateForeground();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _idleBoundsTimer.Stop();
        Unhook(ref _hookForeground);
        Unhook(ref _hookShow);
        Unhook(ref _hookHide);
        Unhook(ref _hookMove);
        _callback = null;
        _current = null;
    }

    private static void Unhook(ref nint hook)
    {
        if (hook == 0)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(hook);
        hook = 0;
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (idObject != 0 || idChild != 0)
        {
            if (eventType is EventObjectShow or EventObjectHide or EventObjectDestroy)
            {
                return;
            }
        }

        var priority = eventType is EventSystemMoveSizeStart or EventSystemMoveSizeEnd
            ? DispatcherPriority.Render
            : DispatcherPriority.Background;

        _ = _dispatcher.BeginInvoke(priority, () =>
        {
            if (_disposed || !_enabled)
            {
                return;
            }

            switch (eventType)
            {
                case EventSystemForeground:
                case EventObjectShow:
                    EvaluateForeground();
                    break;
                case EventObjectHide:
                case EventObjectDestroy:
                    if (_current is { } cur && (hwnd == cur.DialogHwnd || !_windows.IsWindow(cur.DialogHwnd)))
                    {
                        ClearCurrent();
                    }

                    break;
                case EventSystemMoveSizeStart:
                    if (_current is { } moving && (hwnd == 0 || hwnd == moving.DialogHwnd))
                    {
                        _moving = true;
                        _idleBoundsTimer.Stop();
                        DialogMoveStarted?.Invoke();
                    }

                    break;
                case EventSystemMoveSizeEnd:
                    if (_current is not null)
                    {
                        _moving = false;
                        if (!_idleBoundsTimer.IsEnabled)
                        {
                            _idleBoundsTimer.Start();
                        }

                        SyncCurrentBounds();
                        if (_current is { } ended)
                        {
                            DialogMoveEnded?.Invoke(ended);
                        }
                    }

                    break;
            }
        });
    }

    private void EvaluateForeground()
    {
        if (!_enabled)
        {
            return;
        }

        if (_detector.TryDetectForeground(out var target))
        {
            SetCurrent(target);
            return;
        }

        var hwnd = _windows.GetForegroundWindow();
        if (hwnd != 0)
        {
            var root = NativeMethods.GetAncestor(hwnd, 2); // GA_ROOT
            if (root != 0 && root != hwnd && _detector.TryDetect(root, out target))
            {
                SetCurrent(target);
                return;
            }
        }

        if (_current is { } existing && _windows.IsWindow(existing.DialogHwnd) && _windows.IsWindowVisible(existing.DialogHwnd))
        {
            var fg = _windows.GetForegroundWindow();
            if (fg == existing.DialogHwnd
                || fg == _assistHwnd
                || IsDescendantOf(fg, existing.DialogHwnd)
                || (_assistHwnd != 0 && IsDescendantOf(fg, _assistHwnd)))
            {
                if (!_moving)
                {
                    SyncCurrentBounds();
                }

                return;
            }
        }

        ClearCurrent();
    }

    private bool IsDescendantOf(nint hwnd, nint ancestor)
    {
        while (hwnd != 0)
        {
            if (hwnd == ancestor)
            {
                return true;
            }

            hwnd = NativeMethods.GetParent(hwnd);
        }

        return false;
    }

    private void SetCurrent(FileDialogTarget target)
    {
        var changed = _current is null
            || _current.Value.DialogHwnd != target.DialogHwnd
            || _current.Value.FileNameEditHwnd != target.FileNameEditHwnd;

        var moved = _current is { } prev
            && prev.DialogHwnd == target.DialogHwnd
            && (prev.Left != target.Left
                || prev.Top != target.Top
                || prev.Right != target.Right
                || prev.Bottom != target.Bottom);

        _current = target;
        if (!_moving && !_idleBoundsTimer.IsEnabled)
        {
            _idleBoundsTimer.Start();
        }

        if (changed)
        {
            DialogOpened?.Invoke(target);
        }
        else if (moved && !_moving)
        {
            DialogMoved?.Invoke(target);
        }
    }

    private void SyncCurrentBounds()
    {
        if (_moving || _current is not { } cur)
        {
            return;
        }

        if (!_windows.IsWindow(cur.DialogHwnd) || !_windows.IsWindowVisible(cur.DialogHwnd))
        {
            ClearCurrent();
            return;
        }

        if (!_windows.TryGetWindowRect(cur.DialogHwnd, out var left, out var top, out var right, out var bottom))
        {
            ClearCurrent();
            return;
        }

        if (left == cur.Left && top == cur.Top && right == cur.Right && bottom == cur.Bottom)
        {
            return;
        }

        var updated = cur with { Left = left, Top = top, Right = right, Bottom = bottom };
        _current = updated;
        DialogMoved?.Invoke(updated);
    }

    private void ClearCurrent()
    {
        if (_current is null)
        {
            return;
        }

        _current = null;
        _moving = false;
        _idleBoundsTimer.Stop();
        DialogClosed?.Invoke();
    }

    private static class NativeMethods
    {
        public delegate void WinEventDelegate(
            nint hWinEventHook,
            uint eventType,
            nint hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern nint SetWinEventHook(
            uint eventMin,
            uint eventMax,
            nint hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(nint hWinEventHook);

        [DllImport("user32.dll")]
        public static extern nint GetAncestor(nint hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern nint GetParent(nint hWnd);
    }
}
