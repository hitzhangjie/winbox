using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;
using WinBox.Search.Index;
using WinBox.Search.Query;

namespace WinBox.Search;

public sealed class SearchPlugin : IWinBoxPlugin, ISearchService, IQueryHandler
{
    public const int MatchPriority = 0;

    private readonly InMemoryFileIndex _index = new();
    private readonly SubstringSearchEngine _engine = new();
    private int _started;

    public string Id => "winbox.search";
    public string Name => "Search";
    public string Version => "0.1.0";

    public string HandlerId => Id;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _started, 1);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _started, 0);
        return Task.CompletedTask;
    }

    public bool IsStarted => Interlocked.CompareExchange(ref _started, 0, 0) == 1;

    public Task IndexPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        _index.Upsert(paths);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        var hits = _engine.Search(_index.Snapshot(), query, limit);
        return Task.FromResult(hits);
    }

    public bool TryMatch(string rawInput, [NotNullWhen(true)] out QueryMatch? match)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            match = null;
            return false;
        }

        match = new QueryMatch(
            HandlerId,
            MatchPriority,
            Prefix: string.Empty,
            Payload: rawInput,
            ModeLabel: null);
        return true;
    }

    public async Task<QueryResponse> QueryAsync(QueryMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        var hits = await SearchAsync(match.Payload, limit: 20, cancellationToken).ConfigureAwait(false);
        var items = hits
            .Select(h => new QueryResultItem(
                Id: h.Path,
                Title: h.Name,
                Subtitle: h.Path,
                Payload: h.Path,
                Action: ResultActionKind.OpenPath))
            .ToList();
        return new QueryResponse(items);
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        // Opening files comes later; skeleton only claims the route.
        return Task.CompletedTask;
    }

    private void EnsureStarted()
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Search plugin is not started.");
        }
    }
}
