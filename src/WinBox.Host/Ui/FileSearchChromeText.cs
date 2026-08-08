using WinBox.Search.Query;

namespace WinBox.Host.Ui;

/// <summary>Microcopy for the Listary-style File Search window.</summary>
internal static class FileSearchChromeText
{
    public const string WindowTitle = "WinBox — File Search";
    public const string Placeholder = "Search for files and folders";
    public const string EmptyTitle = "Begin your search by typing a keyword";
    public const string EmptyDetail = "Or pick a type / time filter on the left";
    public const string NoResultsTitle = "No matching files";
    public const string NoResultsDetail = "Try another keyword or clear filters";
    public const string FooterHints = "Enter open  ·  Alt+Enter reveal  ·  Esc close";
    public const string ExpandTooltip = "View more · filters & browse";
    public const string FilterByHeader = "Filter by";
    public const string RecentlyModifiedHeader = "Recently modified";
    public const string AdvancedHeader = "Advanced";
    public const string RarelyUsed = "Rarely used files";
    public const string ColName = "Name";
    public const string ColPath = "Path";
    public const string ColModified = "Date modified";

    public static string IndexedCount(int count) =>
        count == 1 ? "1 item indexed" : $"{count:N0} items indexed";

    public static IReadOnlyList<(string Id, string Label, string Glyph)> TypeFilters { get; } =
    [
        (FileTypeCategories.All, "All", "\uE8A5"),
        (FileTypeCategories.Text, "Text documents", "\uE8A5"),
        (FileTypeCategories.Office, "Office documents", "\uE8A5"),
        (FileTypeCategories.Pdf, "PDF", "\uEA90"),
        (FileTypeCategories.Audio, "Audio", "\uE8D6"),
        (FileTypeCategories.Video, "Video", "\uE714"),
    ];

    public static IReadOnlyList<(string Id, string Label, int? Days)> ModifiedFilters { get; } =
    [
        ("all", "All", null),
        ("1d", "Last 1 day", 1),
        ("7d", "Last 7 days", 7),
        ("30d", "Last 30 days", 30),
        ("365d", "Last 365 days", 365),
    ];
}
