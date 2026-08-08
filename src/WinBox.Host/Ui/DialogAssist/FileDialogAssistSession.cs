using WinBox.Abstractions;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>Pure selection/query state for the file-dialog assist strip (unit-testable).</summary>
public sealed class FileDialogAssistSession
{
    private IReadOnlyList<SearchHit> _results = Array.Empty<SearchHit>();

    public string Query { get; private set; } = string.Empty;

    public IReadOnlyList<SearchHit> Results => _results;

    public int SelectedIndex { get; private set; } = -1;

    public SearchHit? SelectedHit =>
        SelectedIndex >= 0 && SelectedIndex < _results.Count ? _results[SelectedIndex] : null;

    public void SetQuery(string query) => Query = query ?? string.Empty;

    public void SetResults(IReadOnlyList<SearchHit> results)
    {
        _results = results ?? Array.Empty<SearchHit>();
        SelectedIndex = _results.Count > 0 ? 0 : -1;
    }

    public void ClearResults()
    {
        _results = Array.Empty<SearchHit>();
        SelectedIndex = -1;
    }

    public bool MoveSelection(int delta)
    {
        if (_results.Count == 0 || delta == 0)
        {
            return false;
        }

        var next = SelectedIndex < 0 ? 0 : SelectedIndex + delta;
        next = Math.Clamp(next, 0, _results.Count - 1);
        if (next == SelectedIndex)
        {
            return false;
        }

        SelectedIndex = next;
        return true;
    }

    public bool SelectIndex(int index)
    {
        if (index < 0 || index >= _results.Count)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }
}
