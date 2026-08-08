namespace WinBox.Search.Index;

/// <summary>
/// Scanner policy: whether to enter a directory or include a file.
/// Blacklist patterns win over whitelist.
/// </summary>
public sealed class IndexPolicy
{
    private readonly HashSet<string> _includeExtensions;
    private readonly HashSet<string> _excludeExtensions;
    private readonly string[] _includePathPatterns;
    private readonly string[] _excludePathPatterns;
    private readonly bool _hasIncludeExtensions;
    private readonly bool _hasIncludePathPatterns;

    public IndexPolicy(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _includeExtensions = ToExtensionSet(options.IncludeExtensions);
        _excludeExtensions = ToExtensionSet(options.ExcludeExtensions);
        _includePathPatterns = NormalizePatterns(options.IncludePathPatterns);
        _excludePathPatterns = NormalizePatterns(options.ExcludePathPatterns);
        _hasIncludeExtensions = _includeExtensions.Count > 0;
        _hasIncludePathPatterns = _includePathPatterns.Length > 0;
    }

    public bool ShouldEnterDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        if (MatchesAnyPathPattern(directoryPath, _excludePathPatterns))
        {
            return false;
        }

        // Include-path filters apply to files; still walk the tree so nested matches work.
        return true;
    }

    public bool ShouldIncludeFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (MatchesAnyPathPattern(filePath, _excludePathPatterns))
        {
            return false;
        }

        var extension = NormalizeExtension(Path.GetExtension(filePath));
        if (_excludeExtensions.Contains(extension))
        {
            return false;
        }

        if (_hasIncludeExtensions && !_includeExtensions.Contains(extension))
        {
            return false;
        }

        if (_hasIncludePathPatterns && !MatchesAnyPathPattern(filePath, _includePathPatterns))
        {
            return false;
        }

        return true;
    }

    private static HashSet<string> ToExtensionSet(IReadOnlyList<string> extensions)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in extensions)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            set.Add(NormalizeExtension(raw));
        }

        return set;
    }

    private static string[] NormalizePatterns(IReadOnlyList<string> patterns) =>
        patterns
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .ToArray();

    internal static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.StartsWith('.') ? trimmed[1..] : trimmed;
    }

    /// <summary>
    /// Match when any path segment equals the pattern, or the file name equals the pattern.
    /// </summary>
    internal static bool MatchesAnyPathPattern(string path, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return false;
        }

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var pattern in patterns)
        {
            foreach (var segment in segments)
            {
                if (segment.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
