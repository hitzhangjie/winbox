using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;
using WinBox.Search.Index;
using WinBox.Search.Index.Usn;
using WinBox.Search.Query;

namespace WinBox.Search;

public sealed class SearchPlugin : IWinBoxPlugin, ISearchService, IQueryHandler, IDisposable
{
    public const int MatchPriority = 0;

    private readonly InMemoryFileIndex _index = new();
    private readonly FilteredSearchEngine _filteredEngine = new();
    private readonly DirectoryScanner _scanner = new();
    private readonly SqliteFileIndexStore _store;
    private readonly IPathActivation _pathActivation;
    private readonly IUsnJournal _usnJournal;
    private readonly IndexChangeWatcher _watcher;
    private readonly object _optionsGate = new();
    private readonly object _mutateGate = new();
    private IndexOptions _options;
    private int _started;
    private int _dirty;
    private bool _disposed;
    private bool _fullyMemoryResident;
    private int _persistedCount;

    public SearchPlugin(
        IndexOptions? options = null,
        IPathActivation? pathActivation = null,
        SqliteFileIndexStore? store = null,
        IUsnJournal? usnJournal = null)
    {
        _options = IndexOptionsStore.Clone(options ?? new IndexOptions());
        _pathActivation = pathActivation ?? new ProcessPathActivation();
        _store = store ?? new SqliteFileIndexStore();
        _usnJournal = usnJournal ?? new NtfsUsnJournal();
        _watcher = new IndexChangeWatcher(OnWatcherBatch, OnWatcherDirty);
    }

    public string Id => "winbox.search";
    public string Name => "Search";
    public string Version => "0.1.0";

    public string HandlerId => Id;

    /// <summary>Configured scan options (roots / allow-deny / store path).</summary>
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

    public int IndexedCount
    {
        get
        {
            lock (_mutateGate)
            {
                return _persistedCount > 0 || _store.IsOpen ? _persistedCount : _index.Count;
            }
        }
    }

    /// <summary>True when every persisted entry currently fits in the memory cache.</summary>
    public bool IsFullyMemoryResident
    {
        get
        {
            lock (_mutateGate)
            {
                return _fullyMemoryResident;
            }
        }
    }

    /// <summary>Entries currently held in the RAM cache (may be a hot subset).</summary>
    public int MemoryCacheCount => _index.Count;

    /// <summary>Obsolete name: prefer <see cref="IsFullyMemoryResident"/>.</summary>
    public bool IsMemoryHydrated => IsFullyMemoryResident;

    public string? IndexStorePath => _store.IsOpen ? _store.DatabasePath : null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _started, 1);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _watcher.Stop();
        _usnJournal.Close();
        PersistUsnCursorBestEffort();
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

    /// <summary>
    /// Load from SQLite when fingerprint matches; otherwise full rebuild.
    /// Applies USN catch-up when possible, then starts Watcher.
    /// </summary>
    public Task<IndexReadyResult> EnsureIndexReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();

        IndexOptions options;
        lock (_optionsGate)
        {
            options = IndexOptionsStore.Clone(_options);
        }

        var fingerprint = OptionsFingerprint.Compute(options);
        var storeDir = options.ResolveIndexStoreDirectory();

        if (!_store.TryOpen(storeDir))
        {
            // Fall back to default directory once.
            var fallback = IndexOptions.DefaultIndexStoreDirectory;
            if (!storeDir.Equals(fallback, StringComparison.OrdinalIgnoreCase)
                && _store.TryOpen(fallback))
            {
                lock (_optionsGate)
                {
                    var fixedOptions = IndexOptionsStore.Clone(options);
                    _options = new IndexOptions
                    {
                        Roots = fixedOptions.Roots,
                        ExcludeRoots = fixedOptions.ExcludeRoots,
                        IncludeExtensions = fixedOptions.IncludeExtensions,
                        ExcludeExtensions = fixedOptions.ExcludeExtensions,
                        IncludePathPatterns = fixedOptions.IncludePathPatterns,
                        ExcludePathPatterns = fixedOptions.ExcludePathPatterns,
                        Recursive = fixedOptions.Recursive,
                        IndexStoreDirectory = fallback,
                        MaxInMemoryMegabytes = fixedOptions.MaxInMemoryMegabytes,
                    };
                }

                options = Options;
                storeDir = fallback;
            }
            else
            {
                return RebuildInternalAsync(options, fingerprint, cancellationToken);
            }
        }

        try
        {
            var storedFingerprint = _store.GetMeta(SqliteFileIndexStore.MetaOptionsFingerprint);
            if (string.Equals(storedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                ApplyHydrationPolicy(options, preloaded: null);
                CatchUpUsn(options);
                if (Interlocked.Exchange(ref _dirty, 0) != 0)
                {
                    return RebuildInternalAsync(options, fingerprint, cancellationToken);
                }

                StartWatchers(options);
                return Task.FromResult(new IndexReadyResult(IndexReadyKind.LoadedFromStore, IndexedCount));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Corrupt / unreadable → rebuild.
        }

        return RebuildInternalAsync(options, fingerprint, cancellationToken);
    }

    public Task IndexPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        var list = paths?.ToArray() ?? [];
        var maxMb = Options.MaxInMemoryMegabytes;
        lock (_mutateGate)
        {
            // Always refresh the RAM cache (LRU-bounded). SQLite is source of truth when open.
            _index.Upsert(list);
            if (!_store.IsOpen)
            {
                _fullyMemoryResident = true;
                _persistedCount = _index.Count;
            }

            if (_store.IsOpen)
            {
                var entries = list
                    .Where(static p => !string.IsNullOrWhiteSpace(p))
                    .Select(InMemoryFileIndexEntryFromPath)
                    .ToArray();
                _store.Upsert(entries);
                _persistedCount = _store.CountEntries();
                _fullyMemoryResident = IndexMemoryBudget.CanFullyReside(_persistedCount, maxMb);
            }
        }

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

        return RebuildInternalAsync(options, OptionsFingerprint.Compute(options), cancellationToken);
    }

    /// <summary>Replace options and rebuild the in-memory + SQLite index.</summary>
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

        if (IsFullyMemoryResident || !_store.IsOpen)
        {
            var hits = _filteredEngine.Search(_index.SnapshotEntries(), query);
            return Task.FromResult(hits);
        }

        // Partial cache: search SQLite for completeness, then promote hits into the LRU cache.
        var fetchLimit = Math.Min(Math.Max(query.Limit * 20, 200), 2000);
        DateTime? rarelyBefore = query.RarelyUsedOnly
            ? DateTime.UtcNow - FilteredSearchEngine.RarelyUsedThreshold
            : null;
        var candidates = _store.QueryCandidates(
            query.Text,
            query.Extensions,
            query.ModifiedAfterUtc,
            rarelyBefore,
            fetchLimit);
        var storeHits = _filteredEngine.Search(candidates, query);
        PromoteToMemoryCache(candidates.Where(c =>
            storeHits.Any(h => h.Path.Equals(c.FullPath, StringComparison.OrdinalIgnoreCase))));
        return Task.FromResult(storeHits);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Dispose();
        _usnJournal.Close();
        _store.Dispose();
    }

    private Task<IndexReadyResult> RebuildInternalAsync(
        IndexOptions options,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        _watcher.Stop();
        var entries = _scanner.Scan(options, cancellationToken);
        lock (_mutateGate)
        {
            var storeDir = options.ResolveIndexStoreDirectory();
            if (!_store.IsOpen
                || !string.Equals(_store.DirectoryPath, Path.GetFullPath(storeDir), StringComparison.OrdinalIgnoreCase))
            {
                _store.Close();
                if (!_store.TryOpen(storeDir))
                {
                    // Persist failed — keep what we can in memory under budget.
                    ApplyHydrationFromEntries(entries, options);
                    StartWatchers(options);
                    return Task.FromResult(new IndexReadyResult(IndexReadyKind.Rebuilt, IndexedCount));
                }
            }

            try
            {
                _store.ReplaceAll(entries, fingerprint);
                CaptureUsnCursor(options);
                _persistedCount = entries.Count;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                // Memory path may still work under budget.
            }

            ApplyHydrationFromEntries(entries, options);
        }

        Interlocked.Exchange(ref _dirty, 0);
        StartWatchers(options);
        return Task.FromResult(new IndexReadyResult(IndexReadyKind.Rebuilt, IndexedCount));
    }

    private void ApplyHydrationPolicy(IndexOptions options, IReadOnlyList<FileIndexEntry>? preloaded)
    {
        lock (_mutateGate)
        {
            var capacity = IndexMemoryBudget.MaxEntries(options.MaxInMemoryMegabytes);
            _index.SetCapacity(capacity);

            if (!_store.IsOpen && preloaded is null)
            {
                _index.Clear();
                _fullyMemoryResident = false;
                _persistedCount = 0;
                return;
            }

            var count = preloaded?.Count ?? _store.CountEntries();
            _persistedCount = count;
            _fullyMemoryResident = IndexMemoryBudget.CanFullyReside(count, options.MaxInMemoryMegabytes);

            _index.Clear();
            if (count == 0)
            {
                return;
            }

            if (_fullyMemoryResident)
            {
                var entries = preloaded ?? _store.LoadAll();
                _index.Upsert(entries);
                return;
            }

            // Seed cache with hottest paths that fit; misses go to SQLite.
            var seed = preloaded is not null
                ? preloaded
                    .OrderByDescending(static e => e.LastAccessTimeUtc)
                    .ThenByDescending(static e => e.LastWriteTimeUtc)
                    .Take(capacity)
                    .ToArray()
                : _store.LoadHottest(capacity);
            _index.Upsert(seed);
        }
    }

    private void ApplyHydrationFromEntries(IReadOnlyList<FileIndexEntry> entries, IndexOptions options)
    {
        var capacity = IndexMemoryBudget.MaxEntries(options.MaxInMemoryMegabytes);
        _index.SetCapacity(capacity);
        _persistedCount = entries.Count;
        _fullyMemoryResident = IndexMemoryBudget.CanFullyReside(entries.Count, options.MaxInMemoryMegabytes);
        _index.Clear();

        if (_fullyMemoryResident)
        {
            _index.Upsert(entries);
            return;
        }

        var seed = entries
            .OrderByDescending(static e => e.LastAccessTimeUtc)
            .ThenByDescending(static e => e.LastWriteTimeUtc)
            .Take(capacity);
        _index.Upsert(seed);
    }

    private void PromoteToMemoryCache(IEnumerable<FileIndexEntry> entries)
    {
        lock (_mutateGate)
        {
            _index.Upsert(entries);
        }
    }

    private void StartWatchers(IndexOptions options)
    {
        _watcher.Start(options.Roots, options.Recursive);
    }

    private void OnWatcherDirty() => Interlocked.Exchange(ref _dirty, 1);

    private void OnWatcherBatch(IReadOnlyList<IndexDelta> batch)
    {
        if (!IsStarted || batch.Count == 0)
        {
            return;
        }

        IndexOptions options;
        lock (_optionsGate)
        {
            options = IndexOptionsStore.Clone(_options);
        }

        var policy = new IndexPolicy(options);
        var upserts = new List<FileIndexEntry>();
        var removes = new List<string>();

        foreach (var delta in batch)
        {
            switch (delta.Kind)
            {
                case IndexDeltaKind.Remove:
                    removes.Add(delta.Path);
                    break;
                case IndexDeltaKind.Upsert:
                    if (!policy.ShouldIncludeFile(delta.Path))
                    {
                        removes.Add(delta.Path);
                        break;
                    }

                    upserts.Add(InMemoryFileIndexEntryFromPath(delta.Path));
                    break;
            }
        }

        lock (_mutateGate)
        {
            if (removes.Count > 0)
            {
                foreach (var path in removes)
                {
                    _index.Remove(path);
                }

                if (_store.IsOpen)
                {
                    _store.Remove(removes);
                    _persistedCount = _store.CountEntries();
                }
            }

            if (upserts.Count > 0)
            {
                _index.Upsert(upserts);

                if (_store.IsOpen)
                {
                    _store.Upsert(upserts);
                    _persistedCount = _store.CountEntries();
                }
            }

            if (_store.IsOpen)
            {
                _fullyMemoryResident = IndexMemoryBudget.CanFullyReside(
                    _persistedCount,
                    options.MaxInMemoryMegabytes);
            }
        }

        if (Interlocked.CompareExchange(ref _dirty, 0, 1) == 1)
        {
            _ = RebuildIndexAsync();
        }
    }

    private void CatchUpUsn(IndexOptions options)
    {
        if (!_store.TryGetUsnCursor(out var volume, out var journalIdRaw, out var nextUsn))
        {
            CaptureUsnCursor(options);
            return;
        }

        if (!ulong.TryParse(journalIdRaw, out var journalId))
        {
            CaptureUsnCursor(options);
            return;
        }

        var sampleRoot = options.Roots.FirstOrDefault(static r => !string.IsNullOrWhiteSpace(r));
        if (sampleRoot is null)
        {
            return;
        }

        if (!_usnJournal.TryOpen(sampleRoot, out var opened, out _))
        {
            // Keep Watcher as primary when USN unavailable.
            return;
        }

        if (!string.Equals(opened.VolumeRoot, volume, StringComparison.OrdinalIgnoreCase)
            || opened.JournalId != journalId)
        {
            Interlocked.Exchange(ref _dirty, 1);
            return;
        }

        var state = new UsnJournalState(volume, journalId, nextUsn);
        if (!_usnJournal.TryReadChanges(state, out var changes, out var next, out _))
        {
            Interlocked.Exchange(ref _dirty, 1);
            return;
        }

        ApplyUsnChanges(options, changes);
        _store.SetUsnCursor(next.VolumeRoot, next.JournalId.ToString(), next.NextUsn);
    }

    private void ApplyUsnChanges(IndexOptions options, IReadOnlyList<UsnChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var policy = new IndexPolicy(options);
        var upserts = new List<FileIndexEntry>();
        var removes = new List<string>();
        var unresolved = false;

        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case UsnChangeReason.Delete:
                    if (TryResolvePathByFrn(change.FileReferenceNumber, out var delPath))
                    {
                        removes.Add(delPath);
                    }
                    else
                    {
                        unresolved = true;
                    }

                    break;

                case UsnChangeReason.Rename:
                    if (TryResolvePathByFrn(change.FileReferenceNumber, out var oldPath))
                    {
                        removes.Add(oldPath);
                        var dir = Path.GetDirectoryName(oldPath);
                        if (!string.IsNullOrEmpty(dir) && !string.IsNullOrWhiteSpace(change.FileName))
                        {
                            var newPath = Path.Combine(dir, change.FileName);
                            if (policy.ShouldIncludeFile(newPath))
                            {
                                upserts.Add(InMemoryFileIndexEntryFromPath(newPath));
                            }
                        }
                        else
                        {
                            unresolved = true;
                        }
                    }
                    else
                    {
                        unresolved = true;
                    }

                    break;

                case UsnChangeReason.CreateOrUpdate:
                    if (TryResolvePathByFrn(change.FileReferenceNumber, out var knownPath))
                    {
                        if (policy.ShouldIncludeFile(knownPath))
                        {
                            upserts.Add(InMemoryFileIndexEntryFromPath(knownPath));
                        }
                        else
                        {
                            removes.Add(knownPath);
                        }
                    }
                    else
                    {
                        // New FRN without path map — need reconcile.
                        unresolved = true;
                    }

                    break;
            }
        }

        lock (_mutateGate)
        {
            foreach (var path in removes)
            {
                _index.Remove(path);
            }

            if (_store.IsOpen && removes.Count > 0)
            {
                _store.Remove(removes);
                _persistedCount = _store.CountEntries();
            }

            if (upserts.Count > 0)
            {
                _index.Upsert(upserts);

                if (_store.IsOpen)
                {
                    _store.Upsert(upserts);
                    _persistedCount = _store.CountEntries();
                }
            }

            if (_store.IsOpen)
            {
                _fullyMemoryResident = IndexMemoryBudget.CanFullyReside(
                    _persistedCount,
                    options.MaxInMemoryMegabytes);
            }
        }

        if (unresolved)
        {
            Interlocked.Exchange(ref _dirty, 1);
        }
    }

    private bool TryResolvePathByFrn(ulong frn, out string path)
    {
        path = string.Empty;
        if (frn == 0)
        {
            return false;
        }

        if (_index.TryGetPathByFrn(frn, out path!))
        {
            return true;
        }

        if (_store.IsOpen && _store.TryFindByFrn(frn, out var entry))
        {
            path = entry.FullPath;
            PromoteToMemoryCache([entry]);
            return true;
        }

        return false;
    }

    private void CaptureUsnCursor(IndexOptions options)
    {
        var sampleRoot = options.Roots.FirstOrDefault(static r => !string.IsNullOrWhiteSpace(r));
        if (sampleRoot is null || !_store.IsOpen)
        {
            return;
        }

        if (_usnJournal.TryOpen(sampleRoot, out var state, out _))
        {
            _store.SetUsnCursor(state.VolumeRoot, state.JournalId.ToString(), state.NextUsn);
        }
        else
        {
            _store.ClearUsnCursor();
        }
    }

    private void PersistUsnCursorBestEffort()
    {
        try
        {
            if (!_store.IsOpen)
            {
                return;
            }

            IndexOptions options;
            lock (_optionsGate)
            {
                options = IndexOptionsStore.Clone(_options);
            }

            CaptureUsnCursor(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            // ignore on shutdown
        }
    }

    private static FileIndexEntry InMemoryFileIndexEntryFromPath(string path)
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
            // fall through
        }

        return new FileIndexEntry(
            FullPath: path,
            FileName: Path.GetFileName(path),
            Extension: IndexPolicy.NormalizeExtension(Path.GetExtension(path)),
            LastWriteTimeUtc: DateTime.MinValue,
            LastAccessTimeUtc: DateTime.MinValue);
    }

    private void EnsureStarted()
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Search plugin is not started.");
        }
    }
}
