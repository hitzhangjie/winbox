using System.Net;
using System.Text;
using WinBox.Abstractions;
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

    [Fact]
    public async Task QueryAsync_EmptyPayload_ShowsHint()
    {
        var plugin = new AiPlugin(new AiOptions
        {
            BaseUrl = "http://127.0.0.1:11434/v1",
            Model = "gpt-oss:20b",
        });

        Assert.True(plugin.TryMatch("?", out var match));
        var response = await plugin.QueryAsync(match!);

        Assert.Single(response.Items);
        Assert.Equal("Ask AI", response.Items[0].Title);
        Assert.Contains("gpt-oss:20b", response.Items[0].Subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_WithPayload_ShowsSubmitHint_WithoutCallingClient()
    {
        var client = new FakeOpenAiClient("should-not-run");
        var plugin = new AiPlugin(
            new AiOptions { Model = "gpt-oss:20b", BaseUrl = "http://127.0.0.1:11434/v1" },
            client);

        Assert.True(plugin.TryMatch("? say hi", out var match));
        var response = await plugin.QueryAsync(match!);

        Assert.Null(client.LastPrompt);
        Assert.Single(response.Items);
        Assert.Equal("ai-ready", response.Items[0].Id);
        Assert.Equal(ResultActionKind.Submit, response.Items[0].Action);
        Assert.Contains("Press Enter to ask", response.Items[0].Subtitle, StringComparison.Ordinal);
        Assert.Equal("say hi", response.Items[0].Payload);
    }

    [Fact]
    public async Task QueryProgressiveAsync_EmitsThinking_ThenPartials_ThenFinal()
    {
        var client = new FakeOpenAiClient(["Hel", "lo", "!"]);
        var plugin = new AiPlugin(client: client)
        {
            StreamUiThrottle = TimeSpan.Zero,
        };

        Assert.True(plugin.TryMatch("? hi", out var match));
        var snapshots = new List<QueryResponse>();
        await plugin.QueryProgressiveAsync(match!, snapshots.Add);

        Assert.True(snapshots.Count >= 3);
        Assert.Equal("ai-pending", snapshots[0].Items[0].Id);
        Assert.Equal(ResultActionKind.None, snapshots[0].Items[0].Action);
        Assert.Contains(snapshots, s => s.Items[0].Id == "ai-stream");
        Assert.Equal("ai-answer", snapshots[^1].Items[0].Id);
        Assert.Equal("Hello!", snapshots[^1].Items[0].Payload);
        Assert.Equal("Hello!", snapshots[^1].Items[0].Title);
        Assert.True(snapshots[^1].Items[0].Multiline);
        Assert.Equal(ResultActionKind.CopyText, snapshots[^1].Items[0].Action);
    }

    [Fact]
    public async Task QueryProgressiveAsync_ClientFailure_ReturnsErrorRow()
    {
        var client = new FakeOpenAiClient(null, new HttpRequestException("connection refused"));
        var plugin = new AiPlugin(client: client);

        Assert.True(plugin.TryMatch("? boom", out var match));
        var snapshots = new List<QueryResponse>();
        await plugin.QueryProgressiveAsync(match!, snapshots.Add);

        Assert.Equal("ai-error", snapshots[^1].Items[0].Id);
        Assert.Equal(ResultActionKind.None, snapshots[^1].Items[0].Action);
        Assert.Contains("connection refused", snapshots[^1].Items[0].Subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyOptions_UpdatesSnapshot()
    {
        var plugin = new AiPlugin();
        plugin.ApplyOptions(new AiOptions
        {
            BaseUrl = "https://api.deepseek.com/v1",
            Model = "deepseek-chat",
            ApiKey = "sk-test",
        });

        var options = plugin.Options;
        Assert.Equal("https://api.deepseek.com/v1", options.BaseUrl);
        Assert.Equal("deepseek-chat", options.Model);
        Assert.Equal("sk-test", options.ApiKey);
    }

    [Fact]
    public void FirstLine_TruncatesAndUsesFirstLine()
    {
        Assert.Equal("line one", AiPlugin.FirstLine("line one\nline two", 120));
        Assert.Equal("abcdefghij…", AiPlugin.FirstLine("abcdefghijklmnopqrstuvwxyz", 11));
    }

    [Fact]
    public void FormatBody_KeepsNewlines_AndSoftCaps()
    {
        Assert.Equal("a\nb\nc", AiPlugin.FormatBody("a\nb\nc"));
        var longText = new string('x', AiPlugin.DisplayBodyMaxChars + 50);
        var body = AiPlugin.FormatBody(longText);
        Assert.Equal(AiPlugin.DisplayBodyMaxChars, body.Length);
        Assert.EndsWith("…", body);
    }

    private sealed class FakeOpenAiClient : IOpenAiCompatibleClient
    {
        private readonly IReadOnlyList<string> _deltas;
        private readonly Exception? _error;

        public FakeOpenAiClient(string? answer, Exception? error = null)
        {
            _deltas = string.IsNullOrEmpty(answer) ? [] : [answer];
            _error = error;
        }

        public FakeOpenAiClient(IReadOnlyList<string> deltas)
        {
            _deltas = deltas;
        }

        public string? LastPrompt { get; private set; }

        public async Task<string> CompleteAsync(
            AiOptions options,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var delta in StreamAsync(options, prompt, cancellationToken))
            {
                sb.Append(delta);
            }

            return sb.ToString();
        }

        public async IAsyncEnumerable<string> StreamAsync(
            AiOptions options,
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            await Task.Yield();
            if (_error is not null)
            {
                throw _error;
            }

            foreach (var delta in _deltas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return delta;
            }
        }
    }
}

public sealed class AiOptionsStoreTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "winbox-ai-tests", Guid.NewGuid().ToString("N"), "ai.json");
        try
        {
            var store = new AiOptionsStore(path);
            store.Save(new AiOptions
            {
                BaseUrl = "https://api.deepseek.com/v1/",
                Model = "deepseek-chat",
                ApiKey = "sk-secret",
            });

            var loaded = store.LoadOrDefault();
            Assert.Equal("https://api.deepseek.com/v1", loaded.BaseUrl);
            Assert.Equal("deepseek-chat", loaded.Model);
            Assert.Equal("sk-secret", loaded.ApiKey);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Normalize_FillsDefaultsForBlankFields()
    {
        var normalized = AiOptionsStore.Normalize(new AiOptions
        {
            BaseUrl = "  ",
            Model = "",
            ApiKey = "  key  ",
        });

        Assert.Equal(AiPlugin.DefaultBaseUrl.TrimEnd('/'), normalized.BaseUrl);
        Assert.Equal(AiPlugin.DefaultModel, normalized.Model);
        Assert.Equal("key", normalized.ApiKey);
    }

    [Fact]
    public void LoadOrDefault_MissingFile_ReturnsFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), "winbox-ai-missing", Guid.NewGuid().ToString("N"), "missing.json");
        var store = new AiOptionsStore(path);
        var loaded = store.LoadOrDefault();
        Assert.Equal(AiPlugin.DefaultBaseUrl.TrimEnd('/'), loaded.BaseUrl);
        Assert.Equal(AiPlugin.DefaultModel, loaded.Model);
    }
}

public sealed class OpenAiCompatibleClientTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434/v1", "http://127.0.0.1:11434/v1/chat/completions")]
    [InlineData("http://127.0.0.1:11434/v1/", "http://127.0.0.1:11434/v1/chat/completions")]
    [InlineData(
        "http://127.0.0.1:11434/v1/chat/completions",
        "http://127.0.0.1:11434/v1/chat/completions")]
    public void BuildCompletionsUrl_Normalizes(string input, string expected)
    {
        Assert.Equal(expected, OpenAiCompatibleClient.BuildCompletionsUrl(input));
    }

    [Fact]
    public async Task CompleteAsync_PostsChatCompletions_AndParsesContent()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "http://127.0.0.1:11434/v1/chat/completions",
                request.RequestUri!.ToString());
            Assert.Null(request.Headers.Authorization);

            var sse =
                "data: {\"choices\":[{\"delta\":{\"content\":\"pong\"}}]}\n\n" +
                "data: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            };
        });

        using var client = new OpenAiCompatibleClient(handler);
        var answer = await client.CompleteAsync(
            new AiOptions { BaseUrl = "http://127.0.0.1:11434/v1", Model = "gpt-oss:20b" },
            "ping");

        Assert.Equal("pong", answer);
    }

    [Fact]
    public async Task StreamAsync_YieldsTokenDeltas()
    {
        var handler = new StubHandler((_, _) =>
        {
            var sse =
                "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
                "data: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            };
        });

        using var client = new OpenAiCompatibleClient(handler);
        var parts = new List<string>();
        await foreach (var delta in client.StreamAsync(
                           new AiOptions { BaseUrl = "http://127.0.0.1:11434/v1", Model = "x" },
                           "hi"))
        {
            parts.Add(delta);
        }

        Assert.Equal(["Hel", "lo"], parts);
    }

    [Theory]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"x\"}}]}", "x")]
    [InlineData("data: [DONE]", null)]
    [InlineData("", null)]
    public void ParseSseDataPayload_ExtractsDelta(string line, string? expected)
    {
        var deltas = OpenAiCompatibleClient.ParseSseDataPayload(line).ToList();
        if (expected is null)
        {
            Assert.Empty(deltas);
        }
        else
        {
            Assert.Equal([expected], deltas);
        }
    }

    [Fact]
    public async Task CompleteAsync_SendsBearerToken_WhenApiKeySet()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("sk-test", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n",
                    Encoding.UTF8,
                    "text/event-stream"),
            };
        });

        using var client = new OpenAiCompatibleClient(handler);
        var answer = await client.CompleteAsync(
            new AiOptions
            {
                BaseUrl = "https://api.deepseek.com/v1",
                Model = "deepseek-chat",
                ApiKey = "sk-test",
            },
            "hi");

        Assert.Equal("ok", answer);
    }

    [Fact]
    public async Task CompleteAsync_HttpError_ThrowsWithStatus()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream down"),
            });

        using var client = new OpenAiCompatibleClient(handler);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CompleteAsync(
                new AiOptions { BaseUrl = "http://127.0.0.1:11434/v1", Model = "x" },
                "hi"));

        Assert.Contains("502", ex.Message, StringComparison.Ordinal);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request, cancellationToken));
    }
}
