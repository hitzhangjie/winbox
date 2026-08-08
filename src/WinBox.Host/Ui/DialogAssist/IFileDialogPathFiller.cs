namespace WinBox.Host.Ui.DialogAssist;

/// <summary>Writes a full path into an Open/Save dialog's filename field.</summary>
public interface IFileDialogPathFiller
{
    bool TryFill(FileDialogTarget target, string fullPath);
}
