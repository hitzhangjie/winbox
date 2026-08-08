namespace WinBox.Host.Ui.DialogAssist;

/// <summary>
/// Classifies standard Windows common file dialogs (#32770) via child-control heuristics.
/// </summary>
public sealed class FileDialogDetector
{
    public const string CommonDialogClass = "#32770";

    private static readonly string[] FileNameLabels =
    [
        "file name",
        "file &name",
        "文件名",
        "檔案名稱",
        "파일 이름",
        "ファイル名",
    ];

    private static readonly string[] ConfirmButtonLabels =
    [
        "open",
        "&open",
        "save",
        "&save",
        "save as",
        "打开",
        "開啟",
        "保存",
        "另存为",
        "另存為",
        "저장",
        "열기",
        "開く",
        "保存する",
    ];

    private readonly IWindowInspection _windows;

    public FileDialogDetector(IWindowInspection windows)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    public bool TryDetect(nint hwnd, out FileDialogTarget target)
    {
        target = default;
        if (hwnd == 0 || !_windows.IsWindow(hwnd) || !_windows.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (!string.Equals(_windows.GetClassName(hwnd), CommonDialogClass, StringComparison.Ordinal))
        {
            return false;
        }

        if (!_windows.TryGetWindowRect(hwnd, out var left, out var top, out var right, out var bottom))
        {
            return false;
        }

        if (right - left < 200 || bottom - top < 120)
        {
            return false;
        }

        var children = _windows.GetChildWindows(hwnd, recursive: true);
        if (children.Count == 0)
        {
            return false;
        }

        if (!HasConfirmButton(children))
        {
            return false;
        }

        var edit = FindFileNameEdit(children);
        if (edit == 0)
        {
            return false;
        }

        target = new FileDialogTarget(hwnd, edit, left, top, right, bottom);
        return true;
    }

    public bool TryDetectForeground(out FileDialogTarget target) =>
        TryDetect(_windows.GetForegroundWindow(), out target);

    private bool HasConfirmButton(IReadOnlyList<nint> children)
    {
        foreach (var child in children)
        {
            var className = _windows.GetClassName(child);
            if (!className.Equals("Button", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = NormalizeLabel(_windows.GetWindowText(child));
            if (text.Length == 0)
            {
                continue;
            }

            foreach (var label in ConfirmButtonLabels)
            {
                if (text.Equals(label, StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private nint FindFileNameEdit(IReadOnlyList<nint> children)
    {
        // Prefer ComboBoxEx32 used by classic/common file dialogs for the filename field.
        foreach (var child in children)
        {
            var className = _windows.GetClassName(child);
            if (!className.Equals("ComboBoxEx32", StringComparison.OrdinalIgnoreCase)
                && !className.Equals("ComboBox", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsNearFileNameLabel(child, children))
            {
                // Still accept the bottom-most ComboBoxEx32 as a strong signal.
                if (!className.Equals("ComboBoxEx32", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var edit = FindEditDescendant(child, children);
            if (edit != 0)
            {
                return edit;
            }

            // Some hosts expose the combo itself as the value target.
            return child;
        }

        // Fallback: Edit next to a "File name" static label.
        foreach (var child in children)
        {
            if (!_windows.GetClassName(child).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsNearFileNameLabel(child, children))
            {
                return child;
            }
        }

        // Last resort: bottom-most visible Edit in the dialog.
        nint best = 0;
        var bestTop = int.MinValue;
        foreach (var child in children)
        {
            if (!_windows.GetClassName(child).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_windows.IsWindowVisible(child))
            {
                continue;
            }

            if (!_windows.TryGetWindowRect(child, out _, out var top, out _, out _))
            {
                continue;
            }

            if (top >= bestTop)
            {
                bestTop = top;
                best = child;
            }
        }

        return best;
    }

    private nint FindEditDescendant(nint parent, IReadOnlyList<nint> all)
    {
        foreach (var child in all)
        {
            if (!_windows.GetClassName(child).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Approximate "descendant" by checking if child rect is inside parent rect.
            if (!_windows.TryGetWindowRect(parent, out var pl, out var pt, out var pr, out var pb)
                || !_windows.TryGetWindowRect(child, out var cl, out var ct, out var cr, out var cb))
            {
                continue;
            }

            if (cl >= pl && ct >= pt && cr <= pr && cb <= pb)
            {
                return child;
            }
        }

        return 0;
    }

    private bool IsNearFileNameLabel(nint control, IReadOnlyList<nint> children)
    {
        if (!_windows.TryGetWindowRect(control, out var left, out var top, out _, out var bottom))
        {
            return false;
        }

        var midY = (top + bottom) / 2;
        foreach (var child in children)
        {
            var className = _windows.GetClassName(child);
            if (!className.Equals("Static", StringComparison.OrdinalIgnoreCase)
                && !className.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var label = NormalizeLabel(_windows.GetWindowText(child));
            if (!IsFileNameLabel(label))
            {
                continue;
            }

            if (!_windows.TryGetWindowRect(child, out var ll, out var lt, out var lr, out var lb))
            {
                continue;
            }

            var labelMidY = (lt + lb) / 2;
            // Same row, label to the left of the control.
            if (Math.Abs(labelMidY - midY) <= 24 && lr <= left + 8 && ll < left)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFileNameLabel(string label)
    {
        foreach (var expected in FileNameLabels)
        {
            if (label.Equals(expected, StringComparison.OrdinalIgnoreCase)
                || label.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("&", string.Empty, StringComparison.Ordinal).Trim().TrimEnd(':').Trim();
    }
}
