using WinBox.Host.Query;
using WinBox.Search;
using WinBox.Toolbox;

namespace WinBox.Host.Tests;

public sealed class QueryRouterTests
{
    private static async Task<QueryRouter> CreateRouterAsync()
    {
        var search = new SearchPlugin();
        await search.StartAsync();
        await search.IndexPathsAsync(
        [
            @"C:\Users\demo\Documents\report.docx",
            @"D:\Github\winbox\README.md",
        ]);

        return new QueryRouter(
        [
            search,
            new CalculatorPlugin(),
            new ShellPlugin(),
            new WebSearchPlugin(),
            new AiPlugin(),
        ]);
    }

    [Fact]
    public async Task Match_Default_IsFileSearch()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("readme");

        Assert.NotNull(match);
        Assert.Equal("winbox.search", match!.HandlerId);
    }

    [Fact]
    public async Task Match_GooglePrefixWithSpace_IsWeb()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("gg winbox");

        Assert.NotNull(match);
        Assert.Equal("winbox.web", match!.HandlerId);
        Assert.Equal("Google", match.ModeLabel);
        Assert.Equal("winbox", match.Payload);
    }

    [Fact]
    public async Task Match_GoogleWithoutSpace_IsFileSearch()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("google");

        Assert.NotNull(match);
        Assert.Equal("winbox.search", match!.HandlerId);
    }

    [Fact]
    public async Task Match_MathExpression_IsCalculator()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("1+2*3");

        Assert.NotNull(match);
        Assert.Equal("winbox.calculator", match!.HandlerId);
    }

    [Fact]
    public async Task Match_ShellPrefix_IsShell()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("> dir");

        Assert.NotNull(match);
        Assert.Equal("winbox.shell", match!.HandlerId);
        Assert.Equal("CMD", match.ModeLabel);
        Assert.Equal("dir", match.Payload);
    }

    [Fact]
    public async Task Match_AiPrefix_IsAi()
    {
        var router = await CreateRouterAsync();

        var match = router.Match("? hello");

        Assert.NotNull(match);
        Assert.Equal("winbox.ai", match!.HandlerId);
        Assert.Equal("AI", match.ModeLabel);
    }

    [Fact]
    public async Task Query_FileSearch_ReturnsHits()
    {
        var router = await CreateRouterAsync();

        var response = await router.QueryAsync("readme");

        Assert.Contains(response.Items, i => i.Title.Equals("README.md", StringComparison.OrdinalIgnoreCase));
    }
}
