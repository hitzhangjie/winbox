using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Prefix <c>?</c> routes to an LLM provider. Settings panel + streaming OpenAI/Ollama client later.
/// Defaults (planned): base URL local Ollama, model <c>gpt-oss:20b</c>.
/// </summary>
public sealed class AiPlugin : IWinBoxPlugin, IQueryHandler
{
    public const int MatchPriority = 100;

    public const string DefaultBaseUrl = "http://127.0.0.1:11434/v1";
    public const string DefaultModel = "gpt-oss:20b";

    public string Id => "winbox.ai";
    public string Name => "AI";
    public string Version => "0.1.0";
    public string HandlerId => Id;

    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string Model { get; set; } = DefaultModel;

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
        if (string.IsNullOrEmpty(rawInput) || rawInput[0] != '?')
        {
            return false;
        }

        var payload = rawInput.Length > 1 ? rawInput[1..].TrimStart() : string.Empty;
        match = new QueryMatch(
            HandlerId,
            MatchPriority,
            Prefix: "?",
            Payload: payload,
            ModeLabel: "AI");
        return true;
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
                    "ai-hint",
                    "Ask AI",
                    Subtitle: $"{Model} via {BaseUrl}",
                    Action: ResultActionKind.None,
                    IconKey: ResultIconKeys.Ai),
            ]));
        }

        // Streaming OpenAI-compatible client lands with the settings panel.
        var item = new QueryResultItem(
            Id: "ai-stub",
            Title: match.Payload,
            Subtitle: $"AI skeleton — will stream from {Model}",
            Payload: match.Payload,
            Action: ResultActionKind.None,
            IconKey: ResultIconKeys.Ai);
        return Task.FromResult(new QueryResponse([item]));
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
