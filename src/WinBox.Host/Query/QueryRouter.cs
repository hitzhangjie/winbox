using WinBox.Abstractions;

namespace WinBox.Host.Query;

/// <summary>
/// Picks the highest-priority <see cref="IQueryHandler"/> match and forwards query/activate calls.
/// </summary>
public sealed class QueryRouter
{
    private readonly Dictionary<string, IQueryHandler> _handlers;

    public QueryRouter(IEnumerable<IQueryHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToDictionary(h => h.HandlerId, StringComparer.OrdinalIgnoreCase);
    }

    public QueryMatch? Match(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return null;
        }

        QueryMatch? best = null;
        foreach (var handler in _handlers.Values)
        {
            if (!handler.TryMatch(rawInput, out var match) || match is null)
            {
                continue;
            }

            if (best is null || match.Priority > best.Priority)
            {
                best = match;
            }
        }

        return best;
    }

    /// <summary>Typeahead query — never streams LLM; progressive work is Enter-only.</summary>
    public async Task RunQueryAsync(
        string? rawInput,
        Action<QueryResponse> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var match = Match(rawInput);
        if (match is null)
        {
            report(new QueryResponse([]));
            return;
        }

        if (!_handlers.TryGetValue(match.HandlerId, out var handler))
        {
            report(new QueryResponse([]));
            return;
        }

        var response = await handler.QueryAsync(match, cancellationToken).ConfigureAwait(false);
        report(response);
    }

    /// <summary>
    /// Enter-triggered progressive query (AI streaming). Falls back to <see cref="IQueryHandler.QueryAsync"/>.
    /// </summary>
    public async Task RunProgressiveAsync(
        QueryMatch match,
        Action<QueryResponse> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(report);

        if (!_handlers.TryGetValue(match.HandlerId, out var handler))
        {
            report(new QueryResponse([]));
            return;
        }

        if (handler is IProgressiveQueryHandler progressive)
        {
            await progressive
                .QueryProgressiveAsync(match, report, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var response = await handler.QueryAsync(match, cancellationToken).ConfigureAwait(false);
        report(response);
    }

    public async Task<QueryResponse> QueryAsync(
        string? rawInput,
        CancellationToken cancellationToken = default)
    {
        QueryResponse? last = null;
        await RunQueryAsync(
                rawInput,
                snapshot => last = snapshot,
                cancellationToken)
            .ConfigureAwait(false);
        return last ?? new QueryResponse([]);
    }

    public async Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(item);

        if (!_handlers.TryGetValue(match.HandlerId, out var handler))
        {
            return;
        }

        await handler.ActivateAsync(match, item, cancellationToken).ConfigureAwait(false);
    }
}
