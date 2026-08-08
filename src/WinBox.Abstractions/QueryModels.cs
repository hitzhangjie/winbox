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
    ResultActionKind Action = ResultActionKind.None);

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
}
