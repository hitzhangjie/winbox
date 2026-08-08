namespace WinBox.Search.Index;

/// <summary>
/// P1 filename-index record. Extension is a field on the same entry, not a second index.
/// <see cref="FileReferenceNumber"/> supports NTFS USN correlation (0 when unknown).
/// </summary>
public sealed record FileIndexEntry(
    string FullPath,
    string FileName,
    string Extension,
    DateTime LastWriteTimeUtc,
    DateTime LastAccessTimeUtc,
    ulong FileReferenceNumber = 0);
