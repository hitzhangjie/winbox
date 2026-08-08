using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinBox.Toolbox;

/// <summary>
/// HTTP client for OpenAI-compatible chat completions (Ollama <c>/v1</c>, DeepSeek, OpenAI, …).
/// Supports SSE streaming (<c>stream: true</c>).
/// </summary>
public sealed class OpenAiCompatibleClient : IOpenAiCompatibleClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenAiCompatibleClient(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        if (handler is null)
        {
            _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(2) };
            _ownsHttp = true;
        }
        else
        {
            _http = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = timeout ?? TimeSpan.FromMinutes(2),
            };
            _ownsHttp = true;
        }
    }

    /// <summary>Test seam: wrap an existing client without taking ownership.</summary>
    public OpenAiCompatibleClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttp = false;
    }

    public async Task<string> CompleteAsync(
        AiOptions options,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var delta in StreamAsync(options, prompt, cancellationToken).ConfigureAwait(false))
        {
            sb.Append(delta);
        }

        var content = sb.ToString().Trim();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("LLM returned an empty completion.");
        }

        return content;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiOptions options,
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        var normalized = AiOptionsStore.Normalize(options);
        var url = BuildCompletionsUrl(normalized.BaseUrl);
        var body = new ChatCompletionRequest
        {
            Model = normalized.Model,
            Stream = true,
            Messages =
            [
                new ChatMessageDto { Role = "user", Content = prompt },
            ],
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(normalized.ApiKey))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", normalized.ApiKey);
        }

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var detail = Truncate(raw, 240);
            throw new HttpRequestException(
                $"LLM HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {detail}");
        }

        await foreach (var delta in ReadSseDeltasAsync(response, cancellationToken).ConfigureAwait(false))
        {
            yield return delta;
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    public static string BuildCompletionsUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            trimmed = AiPlugin.DefaultBaseUrl.TrimEnd('/');
        }

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed + "/chat/completions";
    }

    /// <summary>Parses OpenAI-style SSE lines into content deltas.</summary>
    public static IEnumerable<string> ParseSseDataPayload(string dataLine)
    {
        if (string.IsNullOrWhiteSpace(dataLine))
        {
            yield break;
        }

        var data = dataLine.Trim();
        if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            data = data[5..].Trim();
        }

        if (data.Length == 0 || data == "[DONE]")
        {
            yield break;
        }

        ChatCompletionChunk? chunk;
        try
        {
            chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, JsonOptions);
        }
        catch (JsonException)
        {
            yield break;
        }

        var delta = chunk?.Choices?
            .Select(static c => c.Delta?.Content)
            .FirstOrDefault(static t => !string.IsNullOrEmpty(t));

        // Non-streaming fallback shape nested in an SSE frame (rare).
        delta ??= chunk?.Choices?
            .Select(static c => c.Message?.Content)
            .FirstOrDefault(static t => !string.IsNullOrEmpty(t));

        if (!string.IsNullOrEmpty(delta))
        {
            yield return delta;
        }
    }

    private static async IAsyncEnumerable<string> ReadSseDeltasAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            foreach (var delta in ParseSseDataPayload(line))
            {
                yield return delta;
            }
        }
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..max] + "…";
    }

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = [];
    }

    private sealed class ChatMessageDto
    {
        public string Role { get; set; } = "user";
        public string? Content { get; set; }
    }

    private sealed class ChatCompletionChunk
    {
        public List<ChatChoiceDto>? Choices { get; set; }
    }

    private sealed class ChatChoiceDto
    {
        public ChatMessageDto? Delta { get; set; }
        public ChatMessageDto? Message { get; set; }
    }
}
