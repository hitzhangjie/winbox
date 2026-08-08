namespace WinBox.Host.Ui;

/// <summary>
/// Pure overlay session state — visible + query text. UI and hotkeys drive this;
/// unit tests cover activate / dismiss without a window.
/// </summary>
public sealed class LauncherOverlayState
{
    public bool IsVisible { get; private set; }

    public string Query { get; private set; } = string.Empty;

    public void Activate()
    {
        IsVisible = true;
        Query = string.Empty;
    }

    public void Dismiss()
    {
        IsVisible = false;
        Query = string.Empty;
    }

    public void SetQuery(string? value) => Query = value ?? string.Empty;
}
