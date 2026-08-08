namespace WinBox.Search.Index;

/// <summary>
/// Bounded in-memory path cache with LRU eviction.
/// When under budget the whole index may reside here; when over budget it holds a hot subset
/// and misses fall back to <see cref="SqliteFileIndexStore"/>.
/// </summary>
public sealed class InMemoryFileIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileIndexEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, string> _frnToPath = new();
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new(StringComparer.OrdinalIgnoreCase);
    private int _capacity = int.MaxValue;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public int Capacity
    {
        get
        {
            lock (_gate)
            {
                return _capacity;
            }
        }
    }

    /// <summary>≤ 0 or <see cref="int.MaxValue"/> means unlimited.</summary>
    public void SetCapacity(int maxEntries)
    {
        lock (_gate)
        {
            _capacity = maxEntries <= 0 ? int.MaxValue : maxEntries;
            TrimToCapacityUnlocked();
        }
    }

    public void Upsert(IEnumerable<FileIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        lock (_gate)
        {
            foreach (var entry in entries)
            {
                UpsertOneUnlocked(entry);
            }
        }
    }

    public bool TryGet(string path, out FileIndexEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        lock (_gate)
        {
            var key = path.Trim();
            if (!_entries.TryGetValue(key, out entry!))
            {
                return false;
            }

            TouchUnlocked(key);
            return true;
        }
    }

    public bool TryGetPathByFrn(ulong frn, out string path)
    {
        lock (_gate)
        {
            if (!_frnToPath.TryGetValue(frn, out path!))
            {
                return false;
            }

            TouchUnlocked(path);
            return true;
        }
    }

    /// <summary>
    /// Upsert by path string. Reads disk metadata when the file exists; otherwise synthesizes a record.
    /// </summary>
    public void Upsert(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var batch = new List<FileIndexEntry>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            batch.Add(FromPath(path.Trim()));
        }

        Upsert(batch);
    }

    public bool Remove(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        lock (_gate)
        {
            return RemoveUnlocked(path.Trim());
        }
    }

    public IReadOnlyList<(string Path, string Name)> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Values
                .Select(static e => (e.FullPath, e.FileName))
                .ToArray();
        }
    }

    public IReadOnlyList<FileIndexEntry> SnapshotEntries()
    {
        lock (_gate)
        {
            return _entries.Values.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _frnToPath.Clear();
            _lru.Clear();
            _lruNodes.Clear();
        }
    }

    private void UpsertOneUnlocked(FileIndexEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.FullPath))
        {
            return;
        }

        var normalizedPath = entry.FullPath.Trim();
        var stored = entry with { FullPath = normalizedPath };
        if (_entries.TryGetValue(normalizedPath, out var previous)
            && previous.FileReferenceNumber != 0
            && previous.FileReferenceNumber != stored.FileReferenceNumber)
        {
            _frnToPath.Remove(previous.FileReferenceNumber);
        }

        _entries[normalizedPath] = stored;
        if (stored.FileReferenceNumber != 0)
        {
            _frnToPath[stored.FileReferenceNumber] = normalizedPath;
        }

        TouchUnlocked(normalizedPath);
        TrimToCapacityUnlocked();
    }

    private void TouchUnlocked(string key)
    {
        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return;
        }

        var created = _lru.AddFirst(key);
        _lruNodes[key] = created;
    }

    private void TrimToCapacityUnlocked()
    {
        while (_entries.Count > _capacity && _lru.Last is not null)
        {
            var victim = _lru.Last.Value;
            RemoveUnlocked(victim);
        }
    }

    private bool RemoveUnlocked(string key)
    {
        if (_entries.TryGetValue(key, out var existing) && existing.FileReferenceNumber != 0)
        {
            _frnToPath.Remove(existing.FileReferenceNumber);
        }

        if (_lruNodes.Remove(key, out var node))
        {
            _lru.Remove(node);
        }

        return _entries.Remove(key);
    }

    private static FileIndexEntry FromPath(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                return new FileIndexEntry(
                    FullPath: info.FullName,
                    FileName: info.Name,
                    Extension: IndexPolicy.NormalizeExtension(info.Extension),
                    LastWriteTimeUtc: info.LastWriteTimeUtc,
                    LastAccessTimeUtc: info.LastAccessTimeUtc,
                    FileReferenceNumber: FileReferenceNumber.TryRead(info.FullName));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to synthesized entry.
        }

        var fileName = Path.GetFileName(path);
        return new FileIndexEntry(
            FullPath: path,
            FileName: fileName,
            Extension: IndexPolicy.NormalizeExtension(Path.GetExtension(path)),
            LastWriteTimeUtc: DateTime.MinValue,
            LastAccessTimeUtc: DateTime.MinValue,
            FileReferenceNumber: 0);
    }
}
