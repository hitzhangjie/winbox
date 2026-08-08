using WinBox.Toolbox;

namespace WinBox.Toolbox.Tests;

public sealed class CalculatorPluginTests
{
    [Theory]
    [InlineData("1+2", 3)]
    [InlineData("10/4", 2.5)]
    [InlineData("(1+2)*3", 9)]
    public void TryEvaluate_ComputesExpected(string expression, double expected)
    {
        Assert.True(CalculatorPlugin.TryEvaluate(expression, out var value));
        Assert.Equal(expected, value, precision: 10);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("readme")]
    [InlineData("")]
    public void TryEvaluate_RejectsNonExpressions(string expression)
    {
        Assert.False(CalculatorPlugin.TryEvaluate(expression, out _));
    }

    [Fact]
    public async Task QueryAsync_ReturnsResultItem()
    {
        var plugin = new CalculatorPlugin();
        Assert.True(plugin.TryMatch("2+2", out var match));
        var response = await plugin.QueryAsync(match!);

        Assert.Single(response.Items);
        Assert.Equal("4", response.Items[0].Title);
    }
}

public sealed class WebSearchPluginTests
{
    [Fact]
    public void TryMatch_RequiresTrailingSpace()
    {
        var plugin = new WebSearchPlugin();

        Assert.False(plugin.TryMatch("google", out _));
        Assert.True(plugin.TryMatch("google ", out var match));
        Assert.Equal("Google", match!.ModeLabel);
        Assert.Equal(string.Empty, match.Payload);
    }
}

public sealed class ShellPluginTests
{
    [Fact]
    public void TryMatch_StripsPrefix()
    {
        var plugin = new ShellPlugin();

        Assert.True(plugin.TryMatch(">echo hi", out var match));
        Assert.Equal("echo hi", match!.Payload);
        Assert.Equal("CMD", match.ModeLabel);
    }
}

public sealed class AiPluginTests
{
    [Fact]
    public void TryMatch_UsesQuestionPrefix()
    {
        var plugin = new AiPlugin();

        Assert.True(plugin.TryMatch("? why", out var match));
        Assert.Equal("why", match!.Payload);
        Assert.Equal("AI", match.ModeLabel);
    }
}
