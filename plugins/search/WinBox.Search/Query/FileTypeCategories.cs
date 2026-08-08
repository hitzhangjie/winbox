namespace WinBox.Search.Query;

/// <summary>
/// Stable File Search category ids and their extension sets. Host sends category id;
/// Search owns the maps.
/// </summary>
public static class FileTypeCategories
{
    public const string All = "all";
    public const string Text = "text";
    public const string Office = "office";
    public const string Pdf = "pdf";
    public const string Audio = "audio";
    public const string Video = "video";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Maps =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Text] = ["txt", "md", "markdown", "rst", "log", "csv"],
            [Office] =
            [
                "doc", "docx", "xls", "xlsx", "ppt", "pptx",
                "odt", "ods", "odp", "rtf", "pages", "numbers", "key",
            ],
            [Pdf] = ["pdf"],
            [Audio] = ["mp3", "wav", "flac", "aac", "m4a", "ogg", "wma"],
            [Video] = ["mp4", "avi", "mkv", "mov", "wmv", "webm"],
        };

    public static IReadOnlyList<string>? ExtensionsFor(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)
            || string.Equals(categoryId, All, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Maps.TryGetValue(categoryId.Trim(), out var extensions) ? extensions : null;
    }

    public static bool TryGetExtensions(string categoryId, out IReadOnlyList<string> extensions)
    {
        extensions = Array.Empty<string>();
        var resolved = ExtensionsFor(categoryId);
        if (resolved is null)
        {
            return false;
        }

        extensions = resolved;
        return true;
    }
}
