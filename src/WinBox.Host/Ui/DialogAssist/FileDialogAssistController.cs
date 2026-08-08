using System.Windows.Threading;
using WinBox.Abstractions;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>
/// Hosts the file-dialog watcher + assist strip lifecycle for the WinBox process.
/// </summary>
internal sealed class FileDialogAssistController : IDisposable
{
    private readonly UiOptionsStore _uiStore;
    private readonly FileDialogWatcher _watcher;
    private readonly FileDialogAssistBar _bar;
    private bool _disposed;

    public FileDialogAssistController(
        ISearchService search,
        UiOptionsStore uiStore,
        Dispatcher dispatcher,
        IWindowInspection? windows = null,
        IFileDialogPathFiller? filler = null)
    {
        _uiStore = uiStore ?? throw new ArgumentNullException(nameof(uiStore));
        windows ??= new Win32WindowInspection();
        filler ??= new FileDialogPathFiller();

        var detector = new FileDialogDetector(windows);
        _watcher = new FileDialogWatcher(detector, windows, dispatcher);
        _bar = new FileDialogAssistBar(search, filler);

        _watcher.DialogOpened += OnDialogOpened;
        _watcher.DialogMoved += OnDialogMoved;
        _watcher.DialogClosed += OnDialogClosed;
        _watcher.DialogMoveStarted += OnDialogMoveStarted;
        _watcher.DialogMoveEnded += OnDialogMoveEnded;

        RefreshEnabledFromStore();
        _watcher.SetAssistHwnd(_bar.EnsureHwnd());
    }

    public void RefreshEnabledFromStore()
    {
        var enabled = _uiStore.LoadOrDefault().FileDialogAssistEnabled;
        _watcher.Enabled = enabled;
        if (!enabled)
        {
            _bar.Detach();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.DialogOpened -= OnDialogOpened;
        _watcher.DialogMoved -= OnDialogMoved;
        _watcher.DialogClosed -= OnDialogClosed;
        _watcher.DialogMoveStarted -= OnDialogMoveStarted;
        _watcher.DialogMoveEnded -= OnDialogMoveEnded;
        _watcher.Dispose();
        _bar.Detach();
        _bar.Close();
    }

    private void OnDialogOpened(FileDialogTarget target) => _bar.Attach(target);

    private void OnDialogMoved(FileDialogTarget target) => _bar.Reposition(target);

    private void OnDialogClosed() => _bar.Detach();

    private void OnDialogMoveStarted() => _bar.HideWhileDialogMoves();

    private void OnDialogMoveEnded(FileDialogTarget target) => _bar.ShowAfterDialogMoved(target);
}
