using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinBox.Toolbox;

/// <summary>
/// Loads/saves <see cref="WebSearchOptions"/> as JSON. Path is injectable for tests.
/// </summary>
public sealed class WebSearchOptionsStore
{
    private static readonly char[] KeywordSeparators = [',', ';'];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public WebSearchOptionsStore(string filePath)
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
            "web-searches.json");

    public WebSearchOptions LoadOrDefault(WebSearchOptions? fallback = null)
    {
        fallback ??= new WebSearchOptions();

        if (!File.Exists(_filePath))
        {
            return Clone(fallback);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<WebSearchOptionsDto>(json, JsonOptions);
            return dto is null ? Clone(fallback) : dto.ToOptions();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Clone(fallback);
        }
    }

    public void Save(WebSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = Normalize(options);
        var dto = WebSearchOptionsDto.FromOptions(normalized);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static WebSearchOptions Clone(WebSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Normalize(options);
    }

    /// <summary>Split comma/semicolon keyword text from the settings field.</summary>
    public static IReadOnlyList<string> SplitKeywords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static k => k.Length > 0 && !k.Contains(' ', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string JoinKeywords(IEnumerable<string> keywords) =>
        string.Join(", ", keywords ?? []);

    /// <summary>
    /// Trims fields, drops blanks/spaced keywords, and keeps the first occurrence of each
    /// keyword across all entries (case-insensitive).
    /// </summary>
    public static WebSearchOptions Normalize(WebSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<WebSearchEntry>();
        foreach (var raw in options.Entries ?? Array.Empty<WebSearchEntry>())
        {
            var keywords = new List<string>();
            foreach (var keyword in raw.Keywords ?? Array.Empty<string>())
            {
                var trimmed = (keyword ?? string.Empty).Trim();
                if (trimmed.Length == 0 || trimmed.Contains(' ', StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seen.Add(trimmed))
                {
                    continue;
                }

                keywords.Add(trimmed);
            }

            var url = (raw.UrlTemplate ?? string.Empty).Trim();
            if (keywords.Count == 0 || url.Length == 0)
            {
                continue;
            }

            var display = string.IsNullOrWhiteSpace(raw.DisplayName)
                ? keywords[0]
                : raw.DisplayName.Trim();

            entries.Add(new WebSearchEntry(keywords, display, url, raw.Enabled));
        }

        return new WebSearchOptions { Entries = entries };
    }

    private sealed class WebSearchOptionsDto
    {
        public List<WebSearchEntryDto>? Entries { get; set; }

        public static WebSearchOptionsDto FromOptions(WebSearchOptions options) => new()
        {
            Entries = options.Entries
                .Select(static e => new WebSearchEntryDto
                {
                    Keywords = e.Keywords.ToList(),
                    DisplayName = e.DisplayName,
                    UrlTemplate = e.UrlTemplate,
                    Enabled = e.Enabled,
                })
                .ToList(),
        };

        public WebSearchOptions ToOptions() => Normalize(new WebSearchOptions
        {
            Entries = (Entries ?? [])
                .Select(static e => new WebSearchEntry(
                    e.ResolveKeywords(),
                    e.DisplayName ?? string.Empty,
                    e.UrlTemplate ?? string.Empty,
                    e.Enabled))
                .ToArray(),
        });
    }

    private sealed class WebSearchEntryDto
    {
        public List<string>? Keywords { get; set; }

        /// <summary>Legacy single-keyword field from earlier builds.</summary>
        public string? Keyword { get; set; }

        public string? DisplayName { get; set; }
        public string? UrlTemplate { get; set; }
        public bool Enabled { get; set; } = true;

        public IReadOnlyList<string> ResolveKeywords()
        {
            if (Keywords is { Count: > 0 })
            {
                return Keywords;
            }

            return string.IsNullOrWhiteSpace(Keyword) ? [] : [Keyword];
        }
    }
}
