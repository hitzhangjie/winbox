using WinBox.Abstractions;
using WinBox.Search.Index;

namespace WinBox.Search.Query;

/// <summary>
/// Applies type / mtime / rarely-used filters, then substring ranking or mtime browse order.
/// </summary>
public sealed class FilteredSearchEngine
{
    /// <summary>Files not accessed within this window count as rarely used.</summary>
    public static readonly TimeSpan RarelyUsedThreshold = TimeSpan.FromDays(180);

    private readonly SubstringSearchEngine _substring = new();

    public IReadOnlyList<SearchHit> Search(
        IReadOnlyList<FileIndexEntry> entries,
        SearchQuery query,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);

        var now = utcNow ?? DateTime.UtcNow;
        var hasText = !string.IsNullOrWhiteSpace(query.Text);
        var hasExtensions = query.Extensions is { Count: > 0 };
        var hasMtime = query.ModifiedAfterUtc is not null;
        var hasRarely = query.RarelyUsedOnly;

        if (!hasText && !hasExtensions && !hasMtime && !hasRarely)
        {
            return Array.Empty<SearchHit>();
        }

        var filtered = entries.Where(e => MatchesFilters(e, query, now)).ToList();
        if (filtered.Count == 0)
        {
            return Array.Empty<SearchHit>();
        }

        if (hasText)
        {
            var namePath = filtered.Select(static e => (e.FullPath, e.FileName)).ToArray();
            var scored = _substring.Search(namePath, query.Text, query.Limit);
            var byPath = filtered.ToDictionary(static e => e.FullPath, StringComparer.OrdinalIgnoreCase);
            return scored
                .Select(hit =>
                {
                    byPath.TryGetValue(hit.Path, out var entry);
                    return hit with { LastWriteTimeUtc = entry?.LastWriteTimeUtc };
                })
                .ToArray();
        }

        return filtered
            .OrderByDescending(static e => e.LastWriteTimeUtc)
            .ThenBy(static e => e.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(query.Limit)
            .Select(static e => new SearchHit(e.FullPath, e.FileName, Score: 0, e.LastWriteTimeUtc))
            .ToArray();
    }

    private static bool MatchesFilters(FileIndexEntry entry, SearchQuery query, DateTime utcNow)
    {
        if (query.Extensions is { Count: > 0 })
        {
            var ext = entry.Extension;
            var match = false;
            foreach (var allowed in query.Extensions)
            {
                if (string.Equals(ext, NormalizeExt(allowed), StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                    break;
                }
            }

            if (!match)
            {
                return false;
            }
        }

        if (query.ModifiedAfterUtc is { } after && entry.LastWriteTimeUtc < after)
        {
            return false;
        }

        if (query.RarelyUsedOnly)
        {
            var cutoff = utcNow - RarelyUsedThreshold;
            if (entry.LastAccessTimeUtc >= cutoff)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeExt(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed[1..].ToLowerInvariant() : trimmed.ToLowerInvariant();
    }
}
