using WinBox.Search;
using WinBox.Search.Index;
using WinBox.Search.Index.Usn;

namespace WinBox.Search.Tests;

public sealed class SqliteFileIndexStoreTests
{
    [Fact]
    public void ReplaceAll_ThenLoadAll_RoundTrips()
    {
        using var fixture = TempIndexFixture.Create();
        using var store = new SqliteFileIndexStore();
        store.Open(fixture.StoreDirectory);

        var entries = new[]
        {
            new FileIndexEntry(
                Path.Combine(fixture.Root, "a.md"),
                "a.md",
                "md",
                DateTime.UtcNow,
                DateTime.UtcNow,
                FileReferenceNumber: 42),
        };

        store.ReplaceAll(entries, optionsFingerprint: "fp1");
        var loaded = store.LoadAll();

        Assert.Single(loaded);
        Assert.Equal("a.md", loaded[0].FileName);
        Assert.Equal(42UL, loaded[0].FileReferenceNumber);
        Assert.Equal("fp1", store.GetMeta(SqliteFileIndexStore.MetaOptionsFingerprint));
    }

    [Fact]
    public void Upsert_And_Remove_MutateRows()
    {
        using var fixture = TempIndexFixture.Create();
        using var store = new SqliteFileIndexStore();
        store.Open(fixture.StoreDirectory);

        var path = Path.Combine(fixture.Root, "x.txt");
        store.ReplaceAll(
            [new FileIndexEntry(path, "x.txt", "txt", DateTime.UtcNow, DateTime.UtcNow)],
            "fp");

        store.Upsert(
        [
            new FileIndexEntry(path, "x.txt", "txt", DateTime.UtcNow, DateTime.UtcNow, 9),
            new FileIndexEntry(Path.Combine(fixture.Root, "y.txt"), "y.txt", "txt", DateTime.UtcNow, DateTime.UtcNow),
        ]);
        Assert.Equal(2, store.LoadAll().Count);

        store.Remove([path]);
        Assert.Single(store.LoadAll());
    }
}

public sealed class PersistIndexTests
{
    [Fact]
    public async Task EnsureIndexReady_LoadsFromStore_WithoutRescanWhenFingerprintMatches()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("keep.md", "ok");

        using (var first = CreatePlugin(fixture))
        {
            await first.StartAsync();
            var rebuilt = await first.EnsureIndexReadyAsync();
            Assert.Equal(IndexReadyKind.Rebuilt, rebuilt.Kind);
            Assert.Equal(1, rebuilt.Count);
            await first.StopAsync();
        }

        // Delete source file so a rescan would yield 0; load-from-store should still see 1.
        File.Delete(Directory.GetFiles(fixture.Root, "keep.md", SearchOption.AllDirectories).Single());

        using (var second = CreatePlugin(fixture))
        {
            await second.StartAsync();
            var loaded = await second.EnsureIndexReadyAsync();
            Assert.Equal(IndexReadyKind.LoadedFromStore, loaded.Kind);
            Assert.Equal(1, loaded.Count);
            var hits = await second.SearchAsync("keep");
            Assert.Contains(hits, h => h.Name.Equals("keep.md", StringComparison.OrdinalIgnoreCase));
            await second.StopAsync();
        }
    }

    [Fact]
    public async Task EnsureIndexReady_RebuildsWhenFingerprintChanges()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("a.md", "a");

        using var plugin = CreatePlugin(fixture);
        await plugin.StartAsync();
        await plugin.EnsureIndexReadyAsync();
        Assert.Equal(1, plugin.IndexedCount);

        using var other = TempIndexFixture.Create();
        other.WriteFile("b.md", "b");
        await plugin.ApplyOptionsAsync(other.CreateOptions(excludePathPatterns: []));

        Assert.Equal(1, plugin.IndexedCount);
        Assert.Contains(await plugin.SearchAsync("b"), h => h.Name.Equals("b.md", StringComparison.OrdinalIgnoreCase));
        await plugin.StopAsync();
    }

    [Fact]
    public async Task EnsureIndexReady_CorruptDb_FallsBackToRebuild()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("ok.md", "x");
        var dbPath = Path.Combine(fixture.StoreDirectory, SqliteFileIndexStore.DatabaseFileName);
        Directory.CreateDirectory(fixture.StoreDirectory);
        await File.WriteAllTextAsync(dbPath, "not-a-sqlite-database");

        using var plugin = CreatePlugin(fixture);
        await plugin.StartAsync();
        var ready = await plugin.EnsureIndexReadyAsync();

        Assert.Equal(IndexReadyKind.Rebuilt, ready.Kind);
        Assert.Equal(1, ready.Count);
        await plugin.StopAsync();
    }

    private static SearchPlugin CreatePlugin(TempIndexFixture fixture) =>
        new(fixture.CreateOptions(excludePathPatterns: []), usnJournal: new FakeUsnJournal());
}

public sealed class WatcherIncrementalTests
{
    [Fact]
    public async Task Watcher_CreateDeleteRename_UpdatesIndex()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("seed.md", "seed");

        using var plugin = new SearchPlugin(
            fixture.CreateOptions(excludePathPatterns: []),
            usnJournal: new FakeUsnJournal());
        await plugin.StartAsync();
        await plugin.EnsureIndexReadyAsync();
        Assert.Equal(1, plugin.IndexedCount);

        var created = fixture.WriteFile("new-file.md", "hello");
        await WaitForAsync(async () =>
        {
            var hits = await plugin.SearchAsync("new-file");
            return hits.Any(h => h.Name.Equals("new-file.md", StringComparison.OrdinalIgnoreCase));
        });

        File.Delete(created);
        await WaitForAsync(async () =>
        {
            var hits = await plugin.SearchAsync("new-file");
            return !hits.Any(h => h.Name.Equals("new-file.md", StringComparison.OrdinalIgnoreCase));
        });

        var renamedFrom = fixture.WriteFile("old-name.md", "r");
        await WaitForAsync(async () =>
            (await plugin.SearchAsync("old-name")).Any(h => h.Name.Equals("old-name.md", StringComparison.OrdinalIgnoreCase)));

        var renamedTo = Path.Combine(fixture.Root, "renamed.md");
        File.Move(renamedFrom, renamedTo);
        await WaitForAsync(async () =>
            (await plugin.SearchAsync("renamed")).Any(h => h.Name.Equals("renamed.md", StringComparison.OrdinalIgnoreCase)));

        await plugin.StopAsync();
    }

    [Fact]
    public async Task Watcher_IgnoresBlacklistedPaths()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("ok.md", "ok");

        using var plugin = new SearchPlugin(
            fixture.CreateOptions(),
            usnJournal: new FakeUsnJournal());
        await plugin.StartAsync();
        await plugin.EnsureIndexReadyAsync();

        fixture.WriteFile("node_modules/skip.js", "nope");
        await Task.Delay(600);

        var hits = await plugin.SearchAsync("skip");
        Assert.DoesNotContain(hits, h => h.Name.Equals("skip.js", StringComparison.OrdinalIgnoreCase));
        await plugin.StopAsync();
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, int timeoutMs = 5000)
    {
        var start = Environment.TickCount64;
        while (Environment.TickCount64 - start < timeoutMs)
        {
            if (await condition().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        Assert.Fail("Timed out waiting for watcher-driven index update.");
    }
}

public sealed class UsnJournalFacadeTests
{
    [Fact]
    public void FakeUsnJournal_ReadsQueuedChanges()
    {
        var path = @"C:\tmp\tracked.md";
        var frn = 99UL;
        var fake = new FakeUsnJournal(journalId: 7);
        fake.Enqueue(new UsnChange(2, UsnChangeReason.Delete, frn, "tracked.md", null));

        Assert.True(fake.TryOpen(path, out var state, out _));
        state = state with { NextUsn = 1, JournalId = 7 };
        Assert.True(fake.TryReadChanges(state, out var changes, out var next, out _));
        Assert.Single(changes);
        Assert.Equal(UsnChangeReason.Delete, changes[0].Reason);
        Assert.True(next.NextUsn >= 2);
    }

    [Fact]
    public void FakeUsnJournal_JournalLost_ReturnsFalse()
    {
        var fake = new FakeUsnJournal(journalId: 1);
        Assert.True(fake.TryOpen(@"C:\Windows", out var state, out _));
        fake.ForceJournalLost();
        Assert.False(fake.TryReadChanges(state, out _, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public async Task EnsureIndexReady_UsnJournalLost_TriggersRebuild()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("one.md", "1");
        var fake = new FakeUsnJournal(journalId: 3);

        using (var first = new SearchPlugin(fixture.CreateOptions(excludePathPatterns: []), usnJournal: fake))
        {
            await first.StartAsync();
            var ready = await first.EnsureIndexReadyAsync();
            Assert.Equal(IndexReadyKind.Rebuilt, ready.Kind);
            Assert.Equal(1, ready.Count);
            await first.StopAsync();
        }

        fixture.WriteFile("two.md", "2");
        fake.ForceJournalLost();

        using var second = new SearchPlugin(fixture.CreateOptions(excludePathPatterns: []), usnJournal: fake);
        await second.StartAsync();
        var again = await second.EnsureIndexReadyAsync();
        Assert.Equal(IndexReadyKind.Rebuilt, again.Kind);
        Assert.Equal(2, again.Count);
        await second.StopAsync();
    }
}

public sealed class IndexMemoryBudgetTests
{
    [Fact]
    public void MaxEntries_UnlimitedWhenBudgetNonPositive()
    {
        Assert.Equal(int.MaxValue, IndexMemoryBudget.MaxEntries(0));
        Assert.Equal(int.MaxValue, IndexMemoryBudget.MaxEntries(-1));
    }

    [Fact]
    public void CanFullyReside_RespectsMegabyteCap()
    {
        var entriesForTwoMb = IndexMemoryBudget.MaxEntries(1) + 10;
        Assert.False(IndexMemoryBudget.CanFullyReside(entriesForTwoMb, maxInMemoryMegabytes: 1));
        Assert.True(IndexMemoryBudget.CanFullyReside(100, maxInMemoryMegabytes: 512));
    }
}

public sealed class MemoryCacheLruTests
{
    [Fact]
    public void Upsert_EvictsLeastRecentlyUsed_WhenOverCapacity()
    {
        var index = new InMemoryFileIndex();
        index.SetCapacity(2);
        index.Upsert(
        [
            new FileIndexEntry(@"C:\a.txt", "a.txt", "txt", DateTime.UtcNow, DateTime.UtcNow),
            new FileIndexEntry(@"C:\b.txt", "b.txt", "txt", DateTime.UtcNow, DateTime.UtcNow),
        ]);
        Assert.True(index.TryGet(@"C:\a.txt", out _)); // touch a → b becomes LRU
        index.Upsert([new FileIndexEntry(@"C:\c.txt", "c.txt", "txt", DateTime.UtcNow, DateTime.UtcNow)]);

        Assert.Equal(2, index.Count);
        Assert.True(index.TryGet(@"C:\a.txt", out _));
        Assert.True(index.TryGet(@"C:\c.txt", out _));
        Assert.False(index.TryGet(@"C:\b.txt", out _));
    }
}

public sealed class StoreBackedSearchTests
{
    [Fact]
    public async Task LowMemoryBudget_SearchesViaSqlite_AndKeepsHotCache()
    {
        using var fixture = TempIndexFixture.Create();
        var options = fixture.CreateOptions(excludePathPatterns: []);
        options = new IndexOptions
        {
            Roots = options.Roots,
            ExcludePathPatterns = options.ExcludePathPatterns,
            Recursive = true,
            IndexStoreDirectory = fixture.StoreDirectory,
            MaxInMemoryMegabytes = 1,
        };

        var fingerprint = OptionsFingerprint.Compute(options);
        using var store = new SqliteFileIndexStore();
        store.Open(fixture.StoreDirectory);

        var count = IndexMemoryBudget.MaxEntries(1) + 50;
        var entries = new List<FileIndexEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var name = i == 0 ? "needle-unique-name.md" : $"f{i}.txt";
            entries.Add(new FileIndexEntry(
                FullPath: Path.Combine(fixture.Root, name),
                FileName: name,
                Extension: Path.GetExtension(name).TrimStart('.'),
                LastWriteTimeUtc: DateTime.UtcNow.AddMinutes(-i),
                LastAccessTimeUtc: DateTime.UtcNow.AddMinutes(-i)));
        }

        store.ReplaceAll(entries, fingerprint);

        using var plugin = new SearchPlugin(options, store: store, usnJournal: new FakeUsnJournal());
        await plugin.StartAsync();
        var ready = await plugin.EnsureIndexReadyAsync();

        Assert.Equal(IndexReadyKind.LoadedFromStore, ready.Kind);
        Assert.False(plugin.IsFullyMemoryResident);
        Assert.True(plugin.MemoryCacheCount > 0);
        Assert.True(plugin.MemoryCacheCount <= IndexMemoryBudget.MaxEntries(1));
        Assert.Equal(count, plugin.IndexedCount);

        var hits = await plugin.SearchAsync("needle-unique");
        Assert.Contains(hits, h => h.Name.Equals("needle-unique-name.md", StringComparison.OrdinalIgnoreCase));
        await plugin.StopAsync();
    }
}

public sealed class OptionsFingerprintTests
{
    [Fact]
    public void Compute_IgnoresStoreDirectoryAndMemoryBudget()
    {
        var a = new IndexOptions { Roots = [@"D:\a"], IndexStoreDirectory = @"C:\x", MaxInMemoryMegabytes = 128 };
        var b = new IndexOptions { Roots = [@"D:\a"], IndexStoreDirectory = @"C:\y", MaxInMemoryMegabytes = 2048 };
        Assert.Equal(OptionsFingerprint.Compute(a), OptionsFingerprint.Compute(b));
    }

    [Fact]
    public void Compute_ChangesWhenRootsChange()
    {
        var a = new IndexOptions { Roots = [@"D:\a"] };
        var b = new IndexOptions { Roots = [@"D:\b"] };
        Assert.NotEqual(OptionsFingerprint.Compute(a), OptionsFingerprint.Compute(b));
    }
}
