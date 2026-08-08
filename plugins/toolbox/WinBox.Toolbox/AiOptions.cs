namespace WinBox.Toolbox;

/// <summary>
/// OpenAI-compatible chat settings (Ollama, DeepSeek, OpenAI, …).
/// Persisted by <see cref="AiOptionsStore"/>.
/// </summary>
public sealed class AiOptions
{
    public string BaseUrl { get; set; } = AiPlugin.DefaultBaseUrl;

    public string Model { get; set; } = AiPlugin.DefaultModel;

    /// <summary>Optional. Local Ollama usually needs none; cloud providers need a key.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
