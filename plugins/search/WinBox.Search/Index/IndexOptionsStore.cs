using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinBox.Search.Index;

/// <summary>
/// Loads/saves <see cref="IndexOptions"/> as JSON. Path is injectable for tests.
/// </summary>
public sealed class IndexOptionsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public IndexOptionsStore(string filePath)
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
            "index-options.json");

    /// <summary>First-run default until the user saves from the settings panel.</summary>
    public static IndexOptions CreateFirstRunDefault() =>
        IndexOptions.ForDevRoots(@"D:\Github\proposal");

    public IndexOptions LoadOrDefault(IndexOptions? fallback = null)
    {
        fallback ??= CreateFirstRunDefault();

        if (!File.Exists(_filePath))
        {
            return Clone(fallback);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<IndexOptionsDto>(json, JsonOptions);
            return dto is null ? Clone(fallback) : dto.ToOptions();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Clone(fallback);
        }
    }

    public void Save(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dto = IndexOptionsDto.FromOptions(options);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static IndexOptions Clone(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new IndexOptions
        {
            Roots = options.Roots.ToArray(),
            ExcludeRoots = options.ExcludeRoots.ToArray(),
            IncludeExtensions = options.IncludeExtensions.ToArray(),
            ExcludeExtensions = options.ExcludeExtensions.ToArray(),
            IncludePathPatterns = options.IncludePathPatterns.ToArray(),
            ExcludePathPatterns = options.ExcludePathPatterns.ToArray(),
            Recursive = options.Recursive,
            IndexStoreDirectory = options.IndexStoreDirectory ?? string.Empty,
            MaxInMemoryMegabytes = options.MaxInMemoryMegabytes,
        };
    }

    private sealed class IndexOptionsDto
    {
        public List<string>? Roots { get; set; }
        public List<string>? ExcludeRoots { get; set; }
        public List<string>? IncludeExtensions { get; set; }
        public List<string>? ExcludeExtensions { get; set; }
        public List<string>? IncludePathPatterns { get; set; }
        public List<string>? ExcludePathPatterns { get; set; }
        public bool Recursive { get; set; } = true;
        public string? IndexStoreDirectory { get; set; }
        public int? MaxInMemoryMegabytes { get; set; }

        public static IndexOptionsDto FromOptions(IndexOptions options) => new()
        {
            Roots = options.Roots.ToList(),
            ExcludeRoots = options.ExcludeRoots.ToList(),
            IncludeExtensions = options.IncludeExtensions.ToList(),
            ExcludeExtensions = options.ExcludeExtensions.ToList(),
            IncludePathPatterns = options.IncludePathPatterns.ToList(),
            ExcludePathPatterns = options.ExcludePathPatterns.ToList(),
            Recursive = options.Recursive,
            IndexStoreDirectory = string.IsNullOrWhiteSpace(options.IndexStoreDirectory)
                ? null
                : options.IndexStoreDirectory.Trim(),
            MaxInMemoryMegabytes = options.MaxInMemoryMegabytes,
        };

        public IndexOptions ToOptions() => new()
        {
            Roots = NormalizeList(Roots),
            ExcludeRoots = NormalizeList(ExcludeRoots),
            IncludeExtensions = NormalizeList(IncludeExtensions),
            ExcludeExtensions = NormalizeList(ExcludeExtensions),
            IncludePathPatterns = NormalizeList(IncludePathPatterns),
            ExcludePathPatterns = NormalizeList(ExcludePathPatterns).Count > 0
                ? NormalizeList(ExcludePathPatterns)
                : IndexOptions.DefaultExcludePathPatterns.ToArray(),
            Recursive = Recursive,
            IndexStoreDirectory = string.IsNullOrWhiteSpace(IndexStoreDirectory)
                ? string.Empty
                : IndexStoreDirectory.Trim(),
            MaxInMemoryMegabytes = MaxInMemoryMegabytes is null or < 0
                ? IndexOptions.DefaultMaxInMemoryMegabytes
                : MaxInMemoryMegabytes.Value,
        };

        private static IReadOnlyList<string> NormalizeList(List<string>? values) =>
            (values ?? [])
                .Where(static v => !string.IsNullOrWhiteSpace(v))
                .Select(static v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
