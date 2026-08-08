namespace WinBox.Abstractions;

/// <summary>
/// Search capability exposed by the search plugin (and future data sources).
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Upsert explicit file paths into the index (tests / incremental hooks).
    /// Prefer <see cref="RebuildIndexAsync"/> for configured root scans.
    /// </summary>
    Task IndexPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear and rebuild from the plugin's configured roots / policy.
    /// Missing roots are skipped; does not throw solely because a root is absent.
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public sealed record SearchHit(string Path, string Name, double Score);
