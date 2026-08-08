using System.Diagnostics.CodeAnalysis;

namespace WinBox.Abstractions;

/// <summary>
/// A launcher capability that can claim input and produce results.
/// Host routes keystrokes to the highest-priority matching handler.
/// </summary>
public interface IQueryHandler
{
    string HandlerId { get; }

    bool TryMatch(string rawInput, [NotNullWhen(true)] out QueryMatch? match);

    Task<QueryResponse> QueryAsync(QueryMatch match, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked when the user activates a result (Enter / click).
    /// </summary>
    Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default);
}
