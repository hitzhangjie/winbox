namespace WinBox.Toolbox;

/// <summary>OpenAI-compatible <c>/v1/chat/completions</c>.</summary>
public interface IOpenAiCompatibleClient
{
    Task<string> CompleteAsync(
        AiOptions options,
        string prompt,
        CancellationToken cancellationToken = default);

    /// <summary>Yields content token deltas from a streaming completion.</summary>
    IAsyncEnumerable<string> StreamAsync(
        AiOptions options,
        string prompt,
        CancellationToken cancellationToken = default);
}
