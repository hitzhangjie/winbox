namespace WinBox.Search.Index;

/// <summary>
/// Scanner policy: whether to enter a directory or include a file.
/// Evaluation order (first match that denies wins):
/// 1) exclude roots (path prefix)
/// 2) exclude path patterns (segment name)
/// 3) exclude extensions
/// 4) include extensions (if non-empty)
/// 5) include path patterns (if non-empty)
/// </summary>
public sealed class IndexPolicy
{
    private readonly string[] _excludeRoots;
    private readonly HashSet<string> _includeExtensions;
    private readonly HashSet<string> _excludeExtensions;
    private readonly string[] _includePathPatterns;
    private readonly string[] _excludePathPatterns;
    private readonly bool _hasIncludeExtensions;
    private readonly bool _hasIncludePathPatterns;

    public IndexPolicy(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _excludeRoots = NormalizeRootList(options.ExcludeRoots);
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

        if (IsUnderAnyRoot(directoryPath, _excludeRoots))
        {
            return false;
        }

        if (MatchesAnyPathPattern(directoryPath, _excludePathPatterns))
        {
            return false;
        }

        // Include-path filters apply to files; still walk so nested matches work.
        return true;
    }

    public bool ShouldIncludeFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (IsUnderAnyRoot(filePath, _excludeRoots))
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

    private static string[] NormalizeRootList(IReadOnlyList<string> roots) =>
        roots
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizePathKey)
            .Where(static r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
    /// True when <paramref name="path"/> is equal to, or a descendant of, any root.
    /// </summary>
    internal static bool IsUnderAnyRoot(string path, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return false;
        }

        var full = NormalizePathKey(path);
        if (full.Length == 0)
        {
            return false;
        }

        foreach (var root in roots)
        {
            if (root.Length == 0)
            {
                continue;
            }

            if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Require a directory boundary so D:\Foo does not match D:\Foobar.
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string NormalizePathKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(trimmed)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }

    /// <summary>
    /// Match when any path segment equals the pattern (e.g. node_modules, .git).
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
