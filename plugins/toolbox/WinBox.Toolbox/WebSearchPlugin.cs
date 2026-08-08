using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Configurable web-search prefixes (e.g. <c>gg</c> / <c>so</c> + space). Edited via Host settings.
/// </summary>
public sealed class WebSearchPlugin : IWinBoxPlugin, IQueryHandler
{
    public const int MatchPriority = 80;

    private readonly object _gate = new();
    private IReadOnlyList<WebSearchEntry> _entries;

    public WebSearchPlugin(IEnumerable<WebSearchEntry>? entries = null)
    {
        _entries = Array.Empty<WebSearchEntry>();
        ApplyOptions(new WebSearchOptions
        {
            Entries = (entries ?? WebSearchOptions.DefaultEntries()).ToArray(),
        });
    }

    public string Id => "winbox.web";
    public string Name => "Web Search";
    public string Version => "0.1.0";
    public string HandlerId => Id;

    /// <summary>Snapshot of configured entries (including disabled).</summary>
    public WebSearchOptions Options
    {
        get
        {
            lock (_gate)
            {
                return WebSearchOptionsStore.Clone(new WebSearchOptions { Entries = _entries });
            }
        }
    }

    public void ApplyOptions(WebSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var normalized = WebSearchOptionsStore.Normalize(options);
        lock (_gate)
        {
            _entries = normalized.Entries;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public bool TryMatch(string rawInput, [NotNullWhen(true)] out QueryMatch? match)
    {
        match = null;
        if (string.IsNullOrEmpty(rawInput))
        {
            return false;
        }

        IReadOnlyList<WebSearchEntry> entries;
        lock (_gate)
        {
            entries = _entries;
        }

        // Longest keyword first so "gg" wins over a hypothetical "g".
        foreach (var (entry, keyword) in entries
                     .Where(static e => e.Enabled)
                     .SelectMany(static e => e.Keywords.Select(k => (Entry: e, Keyword: k)))
                     .OrderByDescending(static pair => pair.Keyword.Length))
        {
            if (!TryConsumeKeyword(rawInput, keyword, out var payload))
            {
                continue;
            }

            match = new QueryMatch(
                HandlerId,
                MatchPriority,
                Prefix: keyword + " ",
                Payload: payload,
                ModeLabel: entry.DisplayName,
                PreferredSurface: ResultSurface.Browser);
            return true;
        }

        return false;
    }

    public Task<QueryResponse> QueryAsync(QueryMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(match.Payload))
        {
            return Task.FromResult(new QueryResponse(
            [
                new QueryResultItem(
                    "web-hint",
                    $"Search {match.ModeLabel}",
                    Subtitle: "Keep typing your query",
                    Action: ResultActionKind.None,
                    IconKey: ResultIconKeys.Web),
            ], ResultSurface.Browser));
        }

        var url = BuildUrl(match);
        var item = new QueryResultItem(
            Id: "web-open",
            Title: match.Payload,
            Subtitle: $"Open in {match.ModeLabel}",
            Payload: url,
            Action: ResultActionKind.OpenUrl,
            IconKey: ResultIconKeys.Web);
        return Task.FromResult(new QueryResponse([item], ResultSurface.Browser));
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var url = item.Payload;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }

    /// <summary>Builds a search URL from a template using <c>{query}</c> or <c>{0}</c>.</summary>
    public static string FormatUrl(string urlTemplate, string query)
    {
        ArgumentNullException.ThrowIfNull(urlTemplate);
        var encoded = Uri.EscapeDataString(query ?? string.Empty);
        if (urlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
        {
            return urlTemplate.Replace("{query}", encoded, StringComparison.OrdinalIgnoreCase);
        }

        return string.Format(CultureInfo.InvariantCulture, urlTemplate, encoded);
    }

    private string BuildUrl(QueryMatch match)
    {
        IReadOnlyList<WebSearchEntry> entries;
        lock (_gate)
        {
            entries = _entries;
        }

        var keyword = match.Prefix.TrimEnd();
        var entry = entries.FirstOrDefault(e =>
            e.Keywords.Any(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase)));
        if (entry is null)
        {
            entry = entries.First(e =>
                string.Equals(e.DisplayName, match.ModeLabel, StringComparison.OrdinalIgnoreCase));
        }

        return FormatUrl(entry.UrlTemplate, match.Payload);
    }

    private static bool TryConsumeKeyword(string rawInput, string keyword, out string payload)
    {
        payload = string.Empty;
        if (rawInput.Length < keyword.Length + 1)
        {
            return false;
        }

        if (!rawInput.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rawInput.Length == keyword.Length)
        {
            return false;
        }

        if (rawInput[keyword.Length] != ' ')
        {
            return false;
        }

        payload = rawInput[(keyword.Length + 1)..];
        return true;
    }
}
