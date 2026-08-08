using WinBox.Abstractions;
using WinBox.Host;
using WinBox.Search;

namespace WinBox.Host.Tests;

public sealed class PluginRegistryTests
{
    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = new PluginRegistry();
        registry.Register(new SearchPlugin());

        Assert.Throws<InvalidOperationException>(() => registry.Register(new SearchPlugin()));
    }

    [Fact]
    public async Task StartAll_ThenResolveSearchService()
    {
        var registry = new PluginRegistry();
        registry.Register(new SearchPlugin());

        await registry.StartAllAsync();

        var search = registry.GetRequired<ISearchService>();
        await search.IndexPathsAsync([@"C:\temp\hello.txt"]);
        var hits = await search.SearchAsync("hello");

        Assert.Single(hits);
        Assert.Equal("hello.txt", hits[0].Name);

        await registry.StopAllAsync();
    }
}
