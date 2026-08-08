namespace WinBox.Search.Index;

/// <summary>
/// P1 filename-index record. Extension is a field on the same entry, not a second index.
/// </summary>
public sealed record FileIndexEntry(
    string FullPath,
    string FileName,
    string Extension,
    DateTime LastWriteTimeUtc,
    DateTime LastAccessTimeUtc);
