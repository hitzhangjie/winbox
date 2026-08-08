namespace WinBox.Search.Index;

/// <summary>
/// Watches configured roots and queues create/delete/rename deltas with debounce.
/// </summary>
public sealed class IndexChangeWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<string, PendingChange> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<IReadOnlyList<IndexDelta>> _onBatch;
    private readonly Action _onDirty;
    private readonly TimeSpan _debounce;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    public IndexChangeWatcher(
        Action<IReadOnlyList<IndexDelta>> onBatch,
        Action onDirty,
        TimeSpan? debounce = null)
    {
        _onBatch = onBatch ?? throw new ArgumentNullException(nameof(onBatch));
        _onDirty = onDirty ?? throw new ArgumentNullException(nameof(onDirty));
        _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
    }

    public void Start(IEnumerable<string> roots, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(roots);
        Stop();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                FileSystemWatcher watcher;
                try
                {
                    watcher = new FileSystemWatcher(root)
                    {
                        IncludeSubdirectories = recursive,
                        NotifyFilter = NotifyFilters.FileName
                            | NotifyFilters.DirectoryName
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Size,
                        InternalBufferSize = 64 * 1024,
                    };
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                watcher.Created += OnCreatedOrChanged;
                watcher.Changed += OnCreatedOrChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnError;

                try
                {
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
                {
                    watcher.Dispose();
                    continue;
                }

                _watchers.Add(watcher);
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }

                watcher.Dispose();
            }

            _watchers.Clear();
            _pending.Clear();
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
    }

    private void OnCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.FullPath) || Directory.Exists(e.FullPath))
        {
            return;
        }

        Enqueue(e.FullPath, IndexDeltaKind.Upsert, oldPath: null);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.FullPath))
        {
            return;
        }

        Enqueue(e.FullPath, IndexDeltaKind.Remove, oldPath: null);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.FullPath) && string.IsNullOrWhiteSpace(e.OldFullPath))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.OldFullPath))
        {
            Enqueue(e.OldFullPath, IndexDeltaKind.Remove, oldPath: null);
        }

        if (!string.IsNullOrWhiteSpace(e.FullPath) && !Directory.Exists(e.FullPath))
        {
            Enqueue(e.FullPath, IndexDeltaKind.Upsert, oldPath: e.OldFullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e) => _onDirty();

    private void Enqueue(string path, IndexDeltaKind kind, string? oldPath)
    {
        CancellationTokenSource? ctsToStart = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending[path] = new PendingChange(kind, path, oldPath);
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            ctsToStart = _debounceCts;
        }

        _ = FlushAfterDebounceAsync(ctsToStart!);
    }

    private async Task FlushAfterDebounceAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_debounce, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        List<IndexDelta> batch;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_debounceCts, cts))
            {
                return;
            }

            batch = _pending.Values
                .Select(static p => new IndexDelta(p.Kind, p.Path, p.OldPath))
                .ToList();
            _pending.Clear();
        }

        if (batch.Count > 0)
        {
            _onBatch(batch);
        }
    }

    private sealed record PendingChange(IndexDeltaKind Kind, string Path, string? OldPath);
}

public enum IndexDeltaKind
{
    Upsert,
    Remove,
}

public sealed record IndexDelta(IndexDeltaKind Kind, string Path, string? OldPath);
