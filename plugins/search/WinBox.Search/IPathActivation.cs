namespace WinBox.Search;

/// <summary>
/// Opens a path with the shell, or reveals it in Explorer. Injectable for tests.
/// </summary>
public interface IPathActivation
{
    void Open(string path);

    void RevealInFolder(string path);
}
