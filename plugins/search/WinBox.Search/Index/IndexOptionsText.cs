namespace WinBox.Search.Index;

/// <summary>
/// Helpers for editing <see cref="IndexOptions"/> from plain text fields in settings UI.
/// </summary>
public static class IndexOptionsText
{
    public static IReadOnlyList<string> SplitList(string? text, params char[] separators)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var seps = separators.Length > 0 ? separators : [',', ';', '\n', '\r'];
        return text
            .Split(seps, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> SplitExtensions(string? text)
    {
        return SplitList(text, ',', ';', ' ', '\n', '\r')
            .Select(static e => e.StartsWith('.') ? e[1..] : e)
            .Where(static e => e.Length > 0)
            .Select(static e => e.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string JoinLines(IEnumerable<string> values) =>
        string.Join(Environment.NewLine, values);

    public static string JoinComma(IEnumerable<string> values) =>
        string.Join(", ", values);
}
