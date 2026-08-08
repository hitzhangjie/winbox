using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinBox.Toolbox;

/// <summary>
/// Loads/saves <see cref="AiOptions"/> as JSON. Path is injectable for tests.
/// </summary>
public sealed class AiOptionsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public AiOptionsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Config path is required.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public static string DefaultFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinBox",
            "ai-options.json");

    public AiOptions LoadOrDefault(AiOptions? fallback = null)
    {
        fallback ??= new AiOptions();

        if (!File.Exists(_filePath))
        {
            return Clone(fallback);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<AiOptionsDto>(json, JsonOptions);
            return dto is null ? Clone(fallback) : Normalize(dto.ToOptions());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Clone(fallback);
        }
    }

    public void Save(AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = Normalize(options);
        var dto = AiOptionsDto.FromOptions(normalized);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static AiOptions Clone(AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Normalize(options);
    }

    public static AiOptions Normalize(AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var baseUrl = (options.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
        {
            baseUrl = AiPlugin.DefaultBaseUrl.TrimEnd('/');
        }

        var model = (options.Model ?? string.Empty).Trim();
        if (model.Length == 0)
        {
            model = AiPlugin.DefaultModel;
        }

        return new AiOptions
        {
            BaseUrl = baseUrl,
            Model = model,
            ApiKey = options.ApiKey?.Trim() ?? string.Empty,
        };
    }

    private sealed class AiOptionsDto
    {
        public string? BaseUrl { get; set; }
        public string? Model { get; set; }
        public string? ApiKey { get; set; }

        public static AiOptionsDto FromOptions(AiOptions options) => new()
        {
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            ApiKey = string.IsNullOrEmpty(options.ApiKey) ? null : options.ApiKey,
        };

        public AiOptions ToOptions() => new()
        {
            BaseUrl = BaseUrl ?? string.Empty,
            Model = Model ?? string.Empty,
            ApiKey = ApiKey ?? string.Empty,
        };
    }
}
