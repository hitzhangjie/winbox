using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Prefix <c>?</c> routes the rest of the input as a prompt to an OpenAI-compatible LLM
/// (local Ollama by default; DeepSeek / OpenAI / etc. via Settings → AI).
/// Typeahead shows a Submit hint; Enter streams tokens into the launcher results list.
/// </summary>
public sealed class AiPlugin : IWinBoxPlugin, IProgressiveQueryHandler, IDisposable
{
    public const int MatchPriority = 100;

    public const string DefaultBaseUrl = "http://127.0.0.1:11434/v1";
    public const string DefaultModel = "gpt-oss:20b";

    /// <summary>Minimum interval between streaming UI snapshots.</summary>
    public TimeSpan StreamUiThrottle { get; set; } = TimeSpan.FromMilliseconds(50);

    private readonly object _gate = new();
    private readonly IOpenAiCompatibleClient _client;
    private readonly bool _ownsClient;
    private AiOptions _options;

    public AiPlugin(AiOptions? options = null, IOpenAiCompatibleClient? client = null)
    {
        _options = AiOptionsStore.Normalize(options ?? new AiOptions());
        if (client is null)
        {
            _client = new OpenAiCompatibleClient();
            _ownsClient = true;
        }
        else
        {
            _client = client;
            _ownsClient = false;
        }
    }

    public string Id => "winbox.ai";
    public string Name => "AI";
    public string Version => "0.1.0";
    public string HandlerId => Id;

    public AiOptions Options
    {
        get
        {
            lock (_gate)
            {
                return AiOptionsStore.Clone(_options);
            }
        }
    }

    public void ApplyOptions(AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var normalized = AiOptionsStore.Normalize(options);
        lock (_gate)
        {
            _options = normalized;
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

    /// <summary>Typeahead only — never hits the network. Enter runs <see cref="QueryProgressiveAsync"/>.</summary>
    public Task<QueryResponse> QueryAsync(
        QueryMatch match,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        AiOptions options;
        lock (_gate)
        {
            options = AiOptionsStore.Clone(_options);
        }

        if (string.IsNullOrWhiteSpace(match.Payload))
        {
            return Task.FromResult(new QueryResponse(
            [
                new QueryResultItem(
                    "ai-hint",
                    "Ask AI",
                    Subtitle: $"{options.Model} · {options.BaseUrl}",
                    Action: ResultActionKind.None,
                    IconKey: ResultIconKeys.Ai),
            ]));
        }

        return Task.FromResult(new QueryResponse(
        [
            new QueryResultItem(
                Id: "ai-ready",
                Title: FirstLine(match.Payload, maxChars: 120),
                Subtitle: $"Press Enter to ask · {options.Model}",
                Payload: match.Payload,
                Action: ResultActionKind.Submit,
                IconKey: ResultIconKeys.Ai),
        ]));
    }

    public async Task QueryProgressiveAsync(
        QueryMatch match,
        Action<QueryResponse> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        AiOptions options;
        lock (_gate)
        {
            options = AiOptionsStore.Clone(_options);
        }

        if (string.IsNullOrWhiteSpace(match.Payload))
        {
            report(await QueryAsync(match, cancellationToken).ConfigureAwait(false));
            return;
        }

        report(new QueryResponse(
        [
            new QueryResultItem(
                Id: "ai-pending",
                Title: "Thinking…",
                Subtitle: options.Model,
                Payload: match.Payload,
                Action: ResultActionKind.None,
                IconKey: ResultIconKeys.Ai),
        ]));

        try
        {
            var sb = new StringBuilder();
            var lastUi = Stopwatch.StartNew();
            var emitted = false;

            await foreach (var delta in _client
                               .StreamAsync(options, match.Payload, cancellationToken)
                               .ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                sb.Append(delta);
                emitted = true;

                if (lastUi.Elapsed >= StreamUiThrottle || sb.Length <= 24)
                {
                    lastUi.Restart();
                    report(BuildPartial(sb.ToString(), options.Model));
                }
            }

            var answer = sb.ToString().Trim();
            if (!emitted || answer.Length == 0)
            {
                throw new InvalidOperationException("LLM returned an empty completion.");
            }

            report(new QueryResponse(
            [
                new QueryResultItem(
                    Id: "ai-answer",
                    Title: FormatBody(answer),
                    Subtitle: $"Enter to copy · {options.Model}",
                    Payload: answer,
                    Action: ResultActionKind.CopyText,
                    IconKey: ResultIconKeys.Ai,
                    Multiline: true),
            ]));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or OperationCanceledException)
        {
            var message = ex is OperationCanceledException
                ? "LLM request timed out."
                : ex.Message;
            report(new QueryResponse(
            [
                new QueryResultItem(
                    Id: "ai-error",
                    Title: "AI request failed",
                    Subtitle: Truncate(message, 160),
                    Payload: match.Payload,
                    Action: ResultActionKind.None,
                    IconKey: ResultIconKeys.Ai),
            ]));
        }
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Submit is handled by Host via QueryProgressiveAsync; CopyText by Host clipboard.
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_ownsClient && _client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>Soft-cap for on-screen body; full text remains in <see cref="QueryResultItem.Payload"/>.</summary>
    public const int DisplayBodyMaxChars = 8000;

    public static string FormatBody(string text) =>
        TruncatePreserveNewlines(text?.Trim() ?? string.Empty, DisplayBodyMaxChars);

    public static string FirstLine(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lineEnd = text.IndexOfAny(['\r', '\n']);
        var line = lineEnd >= 0 ? text[..lineEnd] : text;
        line = line.Trim();
        if (line.Length == 0)
        {
            line = text.Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim();
        }

        return Truncate(line, maxChars);
    }

    private static QueryResponse BuildPartial(string text, string model)
    {
        var body = FormatBody(text);
        return new QueryResponse(
        [
            new QueryResultItem(
                Id: "ai-stream",
                Title: body,
                Subtitle: $"Streaming… · {model}",
                Payload: text.Trim(),
                Action: ResultActionKind.None,
                IconKey: ResultIconKeys.Ai,
                Multiline: true),
        ]);
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }

    private static string TruncatePreserveNewlines(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }
}
