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

    public async Task<QueryResponse> QueryAsync(
        string? rawInput,
        CancellationToken cancellationToken = default)
    {
        var match = Match(rawInput);
        if (match is null)
        {
            return new QueryResponse([]);
        }

        if (!_handlers.TryGetValue(match.HandlerId, out var handler))
        {
            return new QueryResponse([]);
        }

        return await handler.QueryAsync(match, cancellationToken).ConfigureAwait(false);
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
