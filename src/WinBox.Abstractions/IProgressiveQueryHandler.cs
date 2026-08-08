namespace WinBox.Abstractions;

/// <summary>
/// Query handler that can push intermediate result snapshots (e.g. LLM token streaming).
/// Host prefers this over a single <see cref="IQueryHandler.QueryAsync"/> when available.
/// </summary>
public interface IProgressiveQueryHandler : IQueryHandler
{
    /// <summary>
    /// Emits one or more <see cref="QueryResponse"/> snapshots until complete or cancelled.
    /// The last snapshot should be activation-ready (e.g. <see cref="ResultActionKind.CopyText"/>).
    /// </summary>
    Task QueryProgressiveAsync(
        QueryMatch match,
        Action<QueryResponse> report,
        CancellationToken cancellationToken = default);
}
