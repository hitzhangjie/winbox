using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Custom web-search prefixes (e.g. <c>google</c> / <c>gg</c> + space). Alias settings panel later.
/// </summary>
public sealed class WebSearchPlugin : IWinBoxPlugin, IQueryHandler
{
    public const int MatchPriority = 80;

    private readonly IReadOnlyList<WebSearchAlias> _aliases;

    public WebSearchPlugin(IEnumerable<WebSearchAlias>? aliases = null)
    {
        _aliases = (aliases ?? DefaultAliases()).ToList();
    }

    public string Id => "winbox.web";
    public string Name => "Web Search";
    public string Version => "0.1.0";
    public string HandlerId => Id;

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

        foreach (var alias in _aliases)
        {
            if (!TryConsumeAlias(rawInput, alias.Prefix, out var payload))
            {
                continue;
            }

            match = new QueryMatch(
                HandlerId,
                MatchPriority,
                Prefix: alias.Prefix + " ",
                Payload: payload,
                ModeLabel: alias.DisplayName,
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
                    Action: ResultActionKind.None),
            ], ResultSurface.Browser));
        }

        var url = BuildUrl(match);
        var item = new QueryResultItem(
            Id: "web-open",
            Title: match.Payload,
            Subtitle: $"Open in {match.ModeLabel}",
            Payload: url,
            Action: ResultActionKind.OpenUrl);
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

    private string BuildUrl(QueryMatch match)
    {
        var alias = _aliases.First(a =>
            string.Equals(a.DisplayName, match.ModeLabel, StringComparison.OrdinalIgnoreCase)
            || match.Prefix.StartsWith(a.Prefix, StringComparison.OrdinalIgnoreCase));

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            alias.UrlTemplate,
            Uri.EscapeDataString(match.Payload));
    }

    private static bool TryConsumeAlias(string rawInput, string alias, out string payload)
    {
        payload = string.Empty;
        if (rawInput.Length < alias.Length + 1)
        {
            return false;
        }

        if (!rawInput.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rawInput.Length == alias.Length)
        {
            return false;
        }

        if (rawInput[alias.Length] != ' ')
        {
            return false;
        }

        payload = rawInput[(alias.Length + 1)..];
        return true;
    }

    private static IEnumerable<WebSearchAlias> DefaultAliases()
    {
        yield return new WebSearchAlias("google", "Google", "https://www.google.com/search?q={0}");
        yield return new WebSearchAlias("gg", "Google", "https://www.google.com/search?q={0}");
    }
}

public sealed record WebSearchAlias(string Prefix, string DisplayName, string UrlTemplate);
