using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>
/// Pure overlay session state — visible + raw query + routed mode chrome + dropdown results.
/// </summary>
public sealed class LauncherOverlayState
{
    private IReadOnlyList<QueryResultItem> _results = Array.Empty<QueryResultItem>();

    public bool IsVisible { get; private set; }

    /// <summary>Full text reconstructed for the router (prefix + payload).</summary>
    public string Query { get; private set; } = string.Empty;

    public string? ModeLabel { get; private set; }

    public string Prefix { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public QueryMatch? ActiveMatch { get; private set; }

    public IReadOnlyList<QueryResultItem> Results => _results;

    public int SelectedIndex { get; private set; } = -1;

    public event Action? Changed;

    public void Activate()
    {
        IsVisible = true;
        ClearQuerySurface();
        RaiseChanged();
    }

    public void Dismiss()
    {
        IsVisible = false;
        ClearQuerySurface();
        RaiseChanged();
    }

    public void SetRawQuery(string? value)
    {
        Query = value ?? string.Empty;
        Prefix = string.Empty;
        Payload = Query;
        ModeLabel = null;
        ActiveMatch = null;
        _results = Array.Empty<QueryResultItem>();
        SelectedIndex = -1;
        RaiseChanged();
    }

    public void ApplyMatch(QueryMatch? match, string rawQuery)
    {
        Query = rawQuery;
        ActiveMatch = match;
        if (match is null)
        {
            Prefix = string.Empty;
            Payload = rawQuery;
            ModeLabel = null;
        }
        else
        {
            Prefix = match.Prefix;
            Payload = match.Payload;
            ModeLabel = match.ModeLabel;
        }

        RaiseChanged();
    }

    public void SetResults(IReadOnlyList<QueryResultItem> results)
    {
        _results = results ?? Array.Empty<QueryResultItem>();
        SelectedIndex = _results.Count > 0 ? 0 : -1;
        RaiseChanged();
    }

    public void SelectNext()
    {
        if (_results.Count == 0)
        {
            return;
        }

        SelectedIndex = (SelectedIndex + 1) % _results.Count;
        RaiseChanged();
    }

    public void SelectPrevious()
    {
        if (_results.Count == 0)
        {
            return;
        }

        SelectedIndex = SelectedIndex <= 0 ? _results.Count - 1 : SelectedIndex - 1;
        RaiseChanged();
    }

    public void SetSelectedIndex(int index)
    {
        if (_results.Count == 0)
        {
            SelectedIndex = -1;
            RaiseChanged();
            return;
        }

        SelectedIndex = Math.Clamp(index, 0, _results.Count - 1);
        RaiseChanged();
    }

    public QueryResultItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < _results.Count ? _results[SelectedIndex] : null;

    public string ComposeRawFromPayload(string payload)
    {
        Payload = payload ?? string.Empty;
        Query = Prefix + Payload;
        return Query;
    }

    private void ClearQuerySurface()
    {
        Query = string.Empty;
        Prefix = string.Empty;
        Payload = string.Empty;
        ModeLabel = null;
        ActiveMatch = null;
        _results = Array.Empty<QueryResultItem>();
        SelectedIndex = -1;
    }

    private void RaiseChanged() => Changed?.Invoke();
}
