namespace WinBox.Abstractions;

/// <summary>
/// Search capability exposed by the search plugin (and future data sources).
/// </summary>
public interface ISearchService
{
    Task IndexPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public sealed record SearchHit(string Path, string Name, double Score);
