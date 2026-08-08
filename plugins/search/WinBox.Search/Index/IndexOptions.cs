namespace WinBox.Search.Index;

/// <summary>
/// Scanner / index scope. Edited via Host settings panel; persisted by <see cref="IndexOptionsStore"/>.
/// Deny rules win over allow rules when both apply.
/// </summary>
public sealed class IndexOptions
{
    /// <summary>Default directory-name denylist for noisy / high-churn trees.</summary>
    public static IReadOnlyList<string> DefaultExcludePathPatterns { get; } =
    [
        ".git",
        "node_modules",
        "__pycache__",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "$Recycle.Bin",
        "System Volume Information",
    ];

    /// <summary>Folders to scan (inclusive roots).</summary>
    public IReadOnlyList<string> Roots { get; init; } = [];

    /// <summary>
    /// Folder prefixes to skip even when under a root
    /// (exact folder or any descendant).
    /// </summary>
    public IReadOnlyList<string> ExcludeRoots { get; init; } = [];

    /// <summary>Empty = do not restrict by extension allow-list.</summary>
    public IReadOnlyList<string> IncludeExtensions { get; init; } = [];

    /// <summary>Extensions never indexed. Wins over <see cref="IncludeExtensions"/>.</summary>
    public IReadOnlyList<string> ExcludeExtensions { get; init; } = [];

    /// <summary>Empty = no extra allow filter on path segments.</summary>
    public IReadOnlyList<string> IncludePathPatterns { get; init; } = [];

    /// <summary>Skip when any path segment equals a pattern (e.g. node_modules).</summary>
    public IReadOnlyList<string> ExcludePathPatterns { get; init; } = DefaultExcludePathPatterns;

    public bool Recursive { get; init; } = true;

    /// <summary>
    /// Directory for the SQLite index DB (<c>files.db</c>). Empty uses <see cref="DefaultIndexStoreDirectory"/>.
    /// </summary>
    public string IndexStoreDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Cap for the in-memory LRU cache (megabytes). When the full index exceeds this,
    /// RAM keeps a hot subset; search / FRN misses fall back to SQLite and promote hits.
    /// ≤ 0 = unlimited. Default 512.
    /// </summary>
    public int MaxInMemoryMegabytes { get; init; } = DefaultMaxInMemoryMegabytes;

    public const int DefaultMaxInMemoryMegabytes = 512;

    /// <summary>Default: %LocalAppData%\WinBox\index</summary>
    public static string DefaultIndexStoreDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinBox",
            "index");

    /// <summary>Resolved store directory (never empty).</summary>
    public string ResolveIndexStoreDirectory()
    {
        if (!string.IsNullOrWhiteSpace(IndexStoreDirectory))
        {
            return IndexStoreDirectory.Trim();
        }

        return DefaultIndexStoreDirectory;
    }

    /// <summary>First-run / demo roots.</summary>
    public static IndexOptions ForDevRoots(params string[] roots) => new()
    {
        Roots = roots ?? [],
        ExcludeRoots = [],
        ExcludePathPatterns = DefaultExcludePathPatterns,
        Recursive = true,
        IndexStoreDirectory = string.Empty,
        MaxInMemoryMegabytes = DefaultMaxInMemoryMegabytes,
    };
}
