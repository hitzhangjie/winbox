namespace WinBox.Search.Index;

/// <summary>
/// In-memory path index for MVP. Later: persistent store + USN incremental updates.
/// </summary>
public sealed class InMemoryFileIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileIndexEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

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

    public void Upsert(IEnumerable<FileIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        lock (_gate)
        {
            foreach (var entry in entries)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                var normalizedPath = entry.FullPath.Trim();
                _entries[normalizedPath] = entry with { FullPath = normalizedPath };
            }
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
            return _entries.Remove(path.Trim());
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
        }
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
                    LastAccessTimeUtc: info.LastAccessTimeUtc);
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
            LastAccessTimeUtc: DateTime.MinValue);
    }
}
