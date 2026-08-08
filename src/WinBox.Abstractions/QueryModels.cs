namespace WinBox.Abstractions;

/// <summary>
/// A successful claim on launcher input. <see cref="Payload"/> is what the handler searches/runs;
/// <see cref="Prefix"/> + <see cref="Payload"/> reconstructs the raw box text when using mode chrome.
/// </summary>
public sealed record QueryMatch(
    string HandlerId,
    int Priority,
    string Prefix,
    string Payload,
    string? ModeLabel = null,
    ResultSurface PreferredSurface = ResultSurface.Dropdown);

public sealed record QueryResponse(
    IReadOnlyList<QueryResultItem> Items,
    ResultSurface Surface = ResultSurface.Dropdown);

public sealed record QueryResultItem(
    string Id,
    string Title,
    string? Subtitle = null,
    string? Payload = null,
    ResultActionKind Action = ResultActionKind.None,
    /// <summary>
    /// Host maps this stable key to a glyph. Prefer <see cref="ResultIconKeys"/> values.
    /// Null = Host falls back to an action glyph.
    /// </summary>
    string? IconKey = null);

/// <summary>Stable icon vocabulary shared by plugins (content) and Host (chrome).</summary>
public static class ResultIconKeys
{
    public const string File = "file";
    public const string Folder = "folder";
    public const string Document = "document";
    public const string Markdown = "markdown";
    public const string Code = "code";
    public const string Pdf = "pdf";
    public const string Spreadsheet = "spreadsheet";
    public const string Presentation = "presentation";
    public const string Image = "image";
    public const string Audio = "audio";
    public const string Video = "video";
    public const string Archive = "archive";
    public const string Executable = "executable";
    public const string Calculator = "calculator";
    public const string Shell = "shell";
    public const string Web = "web";
    public const string Ai = "ai";
    public const string Search = "search";
}

public enum ResultSurface
{
    Dropdown = 0,
    Browser = 1,
}

public enum ResultActionKind
{
    None = 0,
    OpenPath = 1,
    OpenUrl = 2,
    RunCommand = 3,
    CopyText = 4,
    /// <summary>Open Explorer with the path selected (containing folder for files).</summary>
    OpenContainingFolder = 5,
}
