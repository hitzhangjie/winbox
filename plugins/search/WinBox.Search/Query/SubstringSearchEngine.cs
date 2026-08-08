using WinBox.Abstractions;

namespace WinBox.Search.Query;

/// <summary>
/// Case-insensitive substring matcher with simple ranking. Later: fuzzy / trigram.
/// </summary>
public sealed class SubstringSearchEngine
{
    public IReadOnlyList<SearchHit> Search(
        IReadOnlyList<(string Path, string Name)> entries,
        string query,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchHit>();
        }

        var needle = query.Trim();

        return entries
            .Select(entry => Score(entry.Path, entry.Name, needle))
            .Where(static hit => hit is not null)
            .Select(static hit => hit!)
            .OrderByDescending(static hit => hit.Score)
            .ThenBy(static hit => hit.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static SearchHit? Score(string path, string name, string query)
    {
        var nameIndex = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (nameIndex >= 0)
        {
            // Prefer prefix matches, then earlier occurrences, then shorter names.
            var score = 100.0 - nameIndex - (name.Length * 0.01);
            if (nameIndex == 0)
            {
                score += 50;
            }

            return new SearchHit(path, name, score);
        }

        var pathIndex = path.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (pathIndex >= 0)
        {
            var score = 40.0 - (pathIndex * 0.01);
            return new SearchHit(path, name, score);
        }

        return null;
    }
}
