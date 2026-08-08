namespace WinBox.Host.Ui.DialogAssist;

/// <summary>A detected standard Open/Save common dialog and its filename edit control.</summary>
public readonly record struct FileDialogTarget(
    nint DialogHwnd,
    nint FileNameEditHwnd,
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);

    public bool IsValid => DialogHwnd != 0 && FileNameEditHwnd != 0 && Width > 0 && Height > 0;
}
