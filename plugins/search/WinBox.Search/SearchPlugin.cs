using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;
using WinBox.Search.Index;
using WinBox.Search.Query;

namespace WinBox.Search;

public sealed class SearchPlugin : IWinBoxPlugin, ISearchService, IQueryHandler
{
    public const int MatchPriority = 0;

    private readonly InMemoryFileIndex _index = new();
    private readonly FilteredSearchEngine _filteredEngine = new();
    private readonly DirectoryScanner _scanner = new();
    private readonly IPathActivation _pathActivation;
    private readonly object _optionsGate = new();
    private IndexOptions _options;
    private int _started;

    public SearchPlugin(IndexOptions? options = null, IPathActivation? pathActivation = null)
    {
        _options = IndexOptionsStore.Clone(options ?? new IndexOptions());
        _pathActivation = pathActivation ?? new ProcessPathActivation();
    }

    public string Id => "winbox.search";
    public string Name => "Search";
    public string Version => "0.1.0";

    public string HandlerId => Id;

    /// <summary>Configured scan options (roots / allow-deny).</summary>
    public IndexOptions Options
    {
        get
        {
            lock (_optionsGate)
            {
                return IndexOptionsStore.Clone(_options);
            }
        }
    }

    public int IndexedCount => _index.Count;

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

    public void ReplaceOptions(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_optionsGate)
        {
            _options = IndexOptionsStore.Clone(options);
        }
    }

    public Task IndexPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        _index.Upsert(paths);
        return Task.CompletedTask;
    }

    public Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();

        IndexOptions options;
        lock (_optionsGate)
        {
            options = IndexOptionsStore.Clone(_options);
        }

        var entries = _scanner.Scan(options, cancellationToken);
        _index.Clear();
        _index.Upsert(entries);
        return Task.CompletedTask;
    }

    /// <summary>Replace options and rebuild the in-memory index.</summary>
    public async Task ApplyOptionsAsync(IndexOptions options, CancellationToken cancellationToken = default)
    {
        ReplaceOptions(options);
        await RebuildIndexAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return SearchAsync(new SearchQuery(Text: query, Limit: limit), cancellationToken);
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        var hits = _filteredEngine.Search(_index.SnapshotEntries(), query);
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
                Action: ResultActionKind.OpenPath,
                IconKey: FileResultIcon.FromPath(h.Path)))
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

        var path = item.Payload ?? item.Id;
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        switch (item.Action)
        {
            case ResultActionKind.OpenPath:
                _pathActivation.Open(path);
                break;
            case ResultActionKind.OpenContainingFolder:
                _pathActivation.RevealInFolder(path);
                break;
        }

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
