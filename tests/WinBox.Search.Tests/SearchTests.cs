using WinBox.Search.Index;
using WinBox.Search.Query;

namespace WinBox.Search.Tests;

public sealed class SubstringSearchEngineTests
{
    private readonly SubstringSearchEngine _engine = new();

    [Fact]
    public void Search_EmptyQuery_ReturnsNoHits()
    {
        var entries = new[] { (@"C:\a\readme.md", "readme.md") };

        var hits = _engine.Search(entries, "   ", limit: 10);

        Assert.Empty(hits);
    }

    [Fact]
    public void Search_PrefersFileNamePrefixMatch()
    {
        var entries = new[]
        {
            (@"C:\docs\winbox-notes.md", "winbox-notes.md"),
            (@"C:\other\notes-winbox.txt", "notes-winbox.txt"),
            (@"C:\path\with\winbox\deep.txt", "deep.txt"),
        };

        var hits = _engine.Search(entries, "winbox", limit: 10);

        Assert.Equal(3, hits.Count);
        Assert.Equal("winbox-notes.md", hits[0].Name);
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        var entries = Enumerable.Range(1, 30)
            .Select(i => ($@"C:\files\file{i}.txt", $"file{i}.txt"))
            .ToArray();

        var hits = _engine.Search(entries, "file", limit: 5);

        Assert.Equal(5, hits.Count);
    }
}

public sealed class IndexPolicyTests
{
    [Fact]
    public void ShouldEnterDirectory_SkipsExcludedSegment()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            ExcludePathPatterns = [".git", "node_modules"],
        });

        Assert.False(policy.ShouldEnterDirectory(@"D:\repo\.git"));
        Assert.False(policy.ShouldEnterDirectory(@"D:\repo\node_modules\pkg"));
        Assert.True(policy.ShouldEnterDirectory(@"D:\repo\src"));
    }

    [Fact]
    public void ShouldEnterDirectory_SkipsExcludeRootsPrefix()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            ExcludeRoots = [@"D:\Github\big"],
            ExcludePathPatterns = [],
        });

        Assert.False(policy.ShouldEnterDirectory(@"D:\Github\big"));
        Assert.False(policy.ShouldEnterDirectory(@"D:\Github\big\src"));
        Assert.True(policy.ShouldEnterDirectory(@"D:\Github\big-sibling"));
        Assert.True(policy.ShouldEnterDirectory(@"D:\Github\other"));
    }

    [Fact]
    public void ShouldIncludeFile_ExcludeExtensionWins()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            IncludeExtensions = ["md", "txt"],
            ExcludeExtensions = ["md"],
            ExcludePathPatterns = [],
        });

        Assert.False(policy.ShouldIncludeFile(@"D:\docs\a.md"));
        Assert.True(policy.ShouldIncludeFile(@"D:\docs\a.txt"));
        Assert.False(policy.ShouldIncludeFile(@"D:\docs\a.go"));
    }

    [Fact]
    public void ShouldIncludeFile_ExcludeExtensions_WithoutIncludeAllowList()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            IncludeExtensions = [],
            ExcludeExtensions = ["exe", "dll"],
            ExcludePathPatterns = [],
        });

        Assert.True(policy.ShouldIncludeFile(@"D:\a\readme.md"));
        Assert.False(policy.ShouldIncludeFile(@"D:\a\tool.exe"));
        Assert.False(policy.ShouldIncludeFile(@"D:\a\lib.dll"));
    }

    [Fact]
    public void ShouldIncludeFile_EmptyIncludeExtensions_AllowsAll()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            IncludeExtensions = [],
            ExcludePathPatterns = [],
        });

        Assert.True(policy.ShouldIncludeFile(@"D:\a\readme.md"));
        Assert.True(policy.ShouldIncludeFile(@"D:\a\main.go"));
    }

    [Fact]
    public void ShouldIncludeFile_SkipsExcludeRoots()
    {
        var policy = new IndexPolicy(new IndexOptions
        {
            ExcludeRoots = [@"D:\Github\proposal\vendor"],
            ExcludePathPatterns = [],
        });

        Assert.False(policy.ShouldIncludeFile(@"D:\Github\proposal\vendor\x.go"));
        Assert.True(policy.ShouldIncludeFile(@"D:\Github\proposal\design.md"));
    }
}

public sealed class DirectoryScannerTests
{
    [Fact]
    public void Scan_IndexesFilesUnderRoot_AndRespectsDenylist()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("readme.md", "# hello");
        fixture.WriteFile("src/main.go", "package main");
        fixture.WriteFile("node_modules/pkg/index.js", "module.exports = {}");
        fixture.WriteFile(".git/config", "gitdir");

        var scanner = new DirectoryScanner();
        var entries = scanner.Scan(new IndexOptions
        {
            Roots = [fixture.Root],
            ExcludePathPatterns = IndexOptions.DefaultExcludePathPatterns,
            Recursive = true,
        });

        var names = entries.Select(e => e.FileName).OrderBy(n => n).ToArray();
        Assert.Equal(["main.go", "readme.md"], names);
        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Extension)));
        Assert.Contains(entries, e => e.Extension.Equals("go", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_IncludeExtensions_FiltersTypes()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("a.md", "md");
        fixture.WriteFile("b.go", "go");
        fixture.WriteFile("c.txt", "txt");

        var scanner = new DirectoryScanner();
        var entries = scanner.Scan(new IndexOptions
        {
            Roots = [fixture.Root],
            IncludeExtensions = ["md", "txt"],
            ExcludePathPatterns = [],
            Recursive = true,
        });

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, e => e.Extension.Equals("go", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_ExcludeRootsAndExtensions()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("keep/a.md", "keep");
        fixture.WriteFile("skip-me/b.md", "skip");
        fixture.WriteFile("keep/c.exe", "bin");

        var skipRoot = Path.Combine(fixture.Root, "skip-me");
        var scanner = new DirectoryScanner();
        var entries = scanner.Scan(new IndexOptions
        {
            Roots = [fixture.Root],
            ExcludeRoots = [skipRoot],
            ExcludeExtensions = ["exe"],
            ExcludePathPatterns = [],
            Recursive = true,
        });

        Assert.Single(entries);
        Assert.Equal("a.md", entries[0].FileName);
    }

    [Fact]
    public void Scan_MissingRoot_ReturnsEmpty()
    {
        var scanner = new DirectoryScanner();
        var entries = scanner.Scan(IndexOptions.ForDevRoots(
            Path.Combine(Path.GetTempPath(), "winbox-missing-root-" + Guid.NewGuid().ToString("N"))));

        Assert.Empty(entries);
    }
}

public sealed class InMemoryFileIndexTests
{
    [Fact]
    public void Upsert_IsCaseInsensitiveOnPath()
    {
        var index = new InMemoryFileIndex();

        index.Upsert([@"C:\Demo\A.txt", @"c:\demo\a.txt"]);

        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void Upsert_Entry_StoresExtensionAndMtime()
    {
        using var fixture = TempIndexFixture.Create();
        var path = fixture.WriteFile("notes.md", "body");
        var info = new FileInfo(path);

        var index = new InMemoryFileIndex();
        index.Upsert(
        [
            new FileIndexEntry(info.FullName, info.Name, "md", info.LastWriteTimeUtc),
        ]);

        var snap = index.SnapshotEntries();
        Assert.Single(snap);
        Assert.Equal("md", snap[0].Extension);
        Assert.Equal(info.LastWriteTimeUtc, snap[0].LastWriteTimeUtc);
    }
}

public sealed class SearchPluginTests
{
    [Fact]
    public async Task Search_BeforeStart_Throws()
    {
        var plugin = new SearchPlugin();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => plugin.SearchAsync("x"));
    }

    [Fact]
    public async Task IndexAndSearch_ReturnsExpectedHit()
    {
        var plugin = new SearchPlugin();
        await plugin.StartAsync();
        await plugin.IndexPathsAsync(
        [
            @"D:\Github\winbox\README.md",
            @"D:\Github\winbox\src\WinBox.Host\Program.cs",
        ]);

        var hits = await plugin.SearchAsync("readme");

        Assert.Contains(hits, hit => hit.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RebuildIndex_FromTempRoot_ThenSearch_CleansUp()
    {
        using var fixture = TempIndexFixture.Create();
        fixture.WriteFile("design-notes.md", "notes");
        fixture.WriteFile("proposal/12345.md", "go proposal");
        fixture.WriteFile("node_modules/ignore.js", "nope");

        var plugin = new SearchPlugin(new IndexOptions
        {
            Roots = [fixture.Root],
            ExcludePathPatterns = IndexOptions.DefaultExcludePathPatterns,
            Recursive = true,
        });

        await plugin.StartAsync();
        await plugin.RebuildIndexAsync();

        Assert.Equal(2, plugin.IndexedCount);

        var hits = await plugin.SearchAsync("proposal");
        Assert.Contains(hits, h => h.Name.Equals("12345.md", StringComparison.OrdinalIgnoreCase));

        var ignored = await plugin.SearchAsync("ignore");
        Assert.DoesNotContain(ignored, h => h.Name.Equals("ignore.js", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RebuildIndex_BeforeStart_Throws()
    {
        var plugin = new SearchPlugin(IndexOptions.ForDevRoots(@"D:\Github\proposal"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => plugin.RebuildIndexAsync());
    }

    [Fact]
    public async Task ApplyOptions_RebuildsAgainstNewRoots()
    {
        using var first = TempIndexFixture.Create();
        using var second = TempIndexFixture.Create();
        first.WriteFile("alpha.md", "a");
        second.WriteFile("beta.md", "b");

        var plugin = new SearchPlugin(new IndexOptions
        {
            Roots = [first.Root],
            ExcludePathPatterns = [],
            Recursive = true,
        });
        await plugin.StartAsync();
        await plugin.RebuildIndexAsync();
        Assert.Equal(1, plugin.IndexedCount);

        await plugin.ApplyOptionsAsync(new IndexOptions
        {
            Roots = [second.Root],
            ExcludePathPatterns = [],
            Recursive = true,
        });

        Assert.Equal(1, plugin.IndexedCount);
        var hits = await plugin.SearchAsync("beta");
        Assert.Contains(hits, h => h.Name.Equals("beta.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(await plugin.SearchAsync("alpha"), h => h.Name.Equals("alpha.md", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class IndexOptionsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsOptions()
    {
        var path = Path.Combine(Path.GetTempPath(), "winbox-index-tests", Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new IndexOptionsStore(path);
            var original = new IndexOptions
            {
                Roots = [@"D:\Github\proposal", @"D:\Github\winbox"],
                ExcludeRoots = [@"D:\Github\proposal\vendor"],
                IncludeExtensions = ["md", "go"],
                ExcludeExtensions = ["exe"],
                IncludePathPatterns = [],
                ExcludePathPatterns = [".git", "node_modules"],
                Recursive = false,
            };

            store.Save(original);
            var loaded = store.LoadOrDefault();

            Assert.Equal(original.Roots, loaded.Roots);
            Assert.Equal(original.ExcludeRoots, loaded.ExcludeRoots);
            Assert.Equal(original.IncludeExtensions, loaded.IncludeExtensions);
            Assert.Equal(original.ExcludeExtensions, loaded.ExcludeExtensions);
            Assert.Equal(original.ExcludePathPatterns, loaded.ExcludePathPatterns);
            Assert.False(loaded.Recursive);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadOrDefault_MissingFile_ReturnsFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), "winbox-index-tests", "missing-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new IndexOptionsStore(path);
        var fallback = IndexOptions.ForDevRoots(@"D:\only-fallback");

        var loaded = store.LoadOrDefault(fallback);

        Assert.Equal([@"D:\only-fallback"], loaded.Roots);
    }
}

public sealed class IndexOptionsTextTests
{
    [Fact]
    public void SplitExtensions_StripsDotsAndDedupes()
    {
        var result = IndexOptionsText.SplitExtensions("md, .GO; txt  md");

        Assert.Equal(["md", "go", "txt"], result);
    }

    [Fact]
    public void SplitList_SplitsLines()
    {
        var result = IndexOptionsText.SplitList(".git\nnode_modules\r\n.bin", '\n', '\r');

        Assert.Contains(".git", result);
        Assert.Contains("node_modules", result);
        Assert.Contains(".bin", result);
    }
}

/// <summary>
/// Creates a unique temp tree for indexing tests and deletes it on dispose.
/// </summary>
internal sealed class TempIndexFixture : IDisposable
{
    private TempIndexFixture(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TempIndexFixture Create()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "winbox-index-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempIndexFixture(root);
    }

    public string WriteFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; leftover dirs under %TEMP%\winbox-index-tests are OK.
        }
    }
}
