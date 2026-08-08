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

    [Fact]
    public void TryMatch_UsesConfiguredKeyword()
    {
        var plugin = new WebSearchPlugin(
        [
            new WebSearchEntry(["so"], "Stack Overflow", "https://stackoverflow.com/search?q={query}"),
            new WebSearchEntry(["yt"], "YouTube", "https://www.youtube.com/results?search_query={query}"),
        ]);

        Assert.True(plugin.TryMatch("so winbox", out var so));
        Assert.Equal("Stack Overflow", so!.ModeLabel);
        Assert.Equal("winbox", so.Payload);

        Assert.True(plugin.TryMatch("yt demo", out var yt));
        Assert.Equal("YouTube", yt!.ModeLabel);
    }

    [Fact]
    public void TryMatch_AcceptsAnyKeywordOnSameEntry()
    {
        var plugin = new WebSearchPlugin(
        [
            new WebSearchEntry(["google", "gg"], "Google", "https://www.google.com/search?q={query}"),
        ]);

        Assert.True(plugin.TryMatch("google hi", out var a));
        Assert.Equal("Google", a!.ModeLabel);
        Assert.True(plugin.TryMatch("gg hi", out var b));
        Assert.Equal("Google", b!.ModeLabel);
    }

    [Fact]
    public void TryMatch_PrefersLongerKeyword()
    {
        var plugin = new WebSearchPlugin(
        [
            new WebSearchEntry(["g", "gg"], "Google", "https://www.google.com/search?q={query}"),
        ]);

        Assert.True(plugin.TryMatch("gg hello", out var match));
        Assert.Equal("gg ", match!.Prefix);
        Assert.Equal("Google", match.ModeLabel);
    }

    [Fact]
    public void TryMatch_SkipsDisabledEntries()
    {
        var plugin = new WebSearchPlugin(
        [
            new WebSearchEntry(["gg"], "Google", "https://www.google.com/search?q={query}", Enabled: false),
        ]);

        Assert.False(plugin.TryMatch("gg hello", out _));
    }

    [Fact]
    public async Task QueryAsync_FormatsQueryPlaceholder()
    {
        var plugin = new WebSearchPlugin(
        [
            new WebSearchEntry(["gg"], "Google", "https://www.google.com/search?q={query}"),
        ]);

        Assert.True(plugin.TryMatch("gg hello world", out var match));
        var response = await plugin.QueryAsync(match!);

        Assert.Equal(
            "https://www.google.com/search?q=hello%20world",
            response.Items[0].Payload);
    }

    [Fact]
    public void ApplyOptions_ReplacesAliases()
    {
        var plugin = new WebSearchPlugin();
        Assert.True(plugin.TryMatch("gg x", out _));

        plugin.ApplyOptions(new WebSearchOptions
        {
            Entries =
            [
                new WebSearchEntry(["bing"], "Bing", "https://www.bing.com/search?q={query}"),
            ],
        });

        Assert.False(plugin.TryMatch("gg x", out _));
        Assert.True(plugin.TryMatch("bing x", out var match));
        Assert.Equal("Bing", match!.ModeLabel);
    }

    [Fact]
    public void FormatUrl_SupportsLegacyZeroPlaceholder()
    {
        var url = WebSearchPlugin.FormatUrl("https://example.com?q={0}", "a b");
        Assert.Equal("https://example.com?q=a%20b", url);
    }
}

public sealed class WebSearchOptionsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winbox-web-{Guid.NewGuid():N}.json");
        try
        {
            var store = new WebSearchOptionsStore(path);
            var original = new WebSearchOptions
            {
                Entries =
                [
                    new WebSearchEntry(["google", "gg"], "Google", "https://www.google.com/search?q={query}"),
                    new WebSearchEntry(["yt"], "YouTube", "https://www.youtube.com/results?search_query={query}", Enabled: false),
                ],
            };

            store.Save(original);
            var loaded = store.LoadOrDefault();

            Assert.Equal(2, loaded.Entries.Count);
            Assert.Equal(new[] { "google", "gg" }, loaded.Entries[0].Keywords);
            Assert.False(loaded.Entries[1].Enabled);
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
    public void LoadOrDefault_AcceptsLegacySingleKeywordJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winbox-web-legacy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "entries": [
                    {
                      "keyword": "so",
                      "displayName": "Stack Overflow",
                      "urlTemplate": "https://stackoverflow.com/search?q={query}",
                      "enabled": true
                    }
                  ]
                }
                """);

            var loaded = new WebSearchOptionsStore(path).LoadOrDefault();
            Assert.Single(loaded.Entries);
            Assert.Equal(new[] { "so" }, loaded.Entries[0].Keywords);
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
    public void Normalize_MergesDuplicatesAcrossEntries()
    {
        var normalized = WebSearchOptionsStore.Normalize(new WebSearchOptions
        {
            Entries =
            [
                new WebSearchEntry(["GG", "google"], "Google", "https://www.google.com/search?q={query}"),
                new WebSearchEntry(["gg", "so"], "Dup", "https://example.com?q={query}"),
                new WebSearchEntry(["bad key"], "Bad", "https://example.com?q={query}"),
                new WebSearchEntry([], "Empty", "https://example.com?q={query}"),
            ],
        });

        Assert.Equal(2, normalized.Entries.Count);
        Assert.Equal(new[] { "GG", "google" }, normalized.Entries[0].Keywords);
        Assert.Equal(new[] { "so" }, normalized.Entries[1].Keywords);
        Assert.Equal("Dup", normalized.Entries[1].DisplayName);
    }

    [Fact]
    public void SplitKeywords_ParsesCommaSeparated()
    {
        var keywords = WebSearchOptionsStore.SplitKeywords(" google, gg ; yt ");
        Assert.Equal(new[] { "google", "gg", "yt" }, keywords);
    }

    [Fact]
    public void SplitKeywords_DedupesCaseInsensitive()
    {
        var keywords = WebSearchOptionsStore.SplitKeywords("GG, gg, Google");
        Assert.Equal(new[] { "GG", "Google" }, keywords);
    }

    [Fact]
    public void LoadOrDefault_MissingFile_UsesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winbox-web-missing-{Guid.NewGuid():N}.json");
        var store = new WebSearchOptionsStore(path);
        var loaded = store.LoadOrDefault();
        Assert.Contains(loaded.Entries, e => e.Keywords.Contains("gg", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(loaded.Entries, e => e.Keywords.Contains("so", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(loaded.Entries, e => e.Keywords.Contains("x", StringComparer.OrdinalIgnoreCase));
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
