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

public sealed class InMemoryFileIndexTests
{
    [Fact]
    public void Upsert_IsCaseInsensitiveOnPath()
    {
        var index = new InMemoryFileIndex();

        index.Upsert([@"C:\Demo\A.txt", @"c:\demo\a.txt"]);

        Assert.Equal(1, index.Count);
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
}
