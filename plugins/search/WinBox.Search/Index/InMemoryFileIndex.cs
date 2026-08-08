namespace WinBox.Search.Index;

/// <summary>
/// In-memory path index for MVP. Later: persistent store + USN incremental updates.
/// </summary>
public sealed class InMemoryFileIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);

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

    public void Upsert(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        lock (_gate)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var normalized = path.Trim();
                _entries[normalized] = System.IO.Path.GetFileName(normalized);
            }
        }
    }

    public IReadOnlyList<(string Path, string Name)> Snapshot()
    {
        lock (_gate)
        {
            return _entries
                .Select(static pair => (pair.Key, pair.Value))
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
