namespace WinBox.Search.Index;

/// <summary>
/// Scanner / index scope. Config UI comes later; model is fixed first.
/// Deny (exclude) wins over allow (include) when both apply.
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

    public IReadOnlyList<string> Roots { get; init; } = [];

    /// <summary>Empty = do not restrict by extension.</summary>
    public IReadOnlyList<string> IncludeExtensions { get; init; } = [];

    public IReadOnlyList<string> ExcludeExtensions { get; init; } = [];

    /// <summary>Empty = no extra allow filter on paths.</summary>
    public IReadOnlyList<string> IncludePathPatterns { get; init; } = [];

    public IReadOnlyList<string> ExcludePathPatterns { get; init; } = DefaultExcludePathPatterns;

    public bool Recursive { get; init; } = true;

    /// <summary>Dev-time roots until a settings panel exists.</summary>
    public static IndexOptions ForDevRoots(params string[] roots) => new()
    {
        Roots = roots ?? [],
        ExcludePathPatterns = DefaultExcludePathPatterns,
        Recursive = true,
    };
}
