namespace WinBox.Toolbox;

/// <summary>
/// One web search with one or more trigger keywords. Edited in Host settings;
/// persisted by <see cref="WebSearchOptionsStore"/>.
/// </summary>
public sealed record WebSearchEntry(
    IReadOnlyList<string> Keywords,
    string DisplayName,
    string UrlTemplate,
    bool Enabled = true);

/// <summary>Configurable web-search aliases (Alfred / Listary style).</summary>
public sealed class WebSearchOptions
{
    public IReadOnlyList<WebSearchEntry> Entries { get; init; } = DefaultEntries();

    public static IReadOnlyList<WebSearchEntry> DefaultEntries() =>
    [
        new(["google", "gg"], "Google", "https://www.google.com/search?q={query}"),
        new(["so"], "Stack Overflow", "https://stackoverflow.com/search?q={query}"),
        new(["yt"], "YouTube", "https://www.youtube.com/results?search_query={query}"),
        new(["x"], "X", "https://x.com/search?q={query}"),
    ];
}
