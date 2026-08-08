namespace WinBox.Search.Index;

public enum IndexReadyKind
{
    LoadedFromStore,
    Rebuilt,
}

public sealed record IndexReadyResult(IndexReadyKind Kind, int Count);
