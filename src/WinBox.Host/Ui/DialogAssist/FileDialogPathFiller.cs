using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>
/// Fills the dialog filename control via UI Automation ValuePattern, with WM_SETTEXT fallback.
/// </summary>
internal sealed class FileDialogPathFiller : IFileDialogPathFiller
{
    private const uint WmSetText = 0x000C;

    public bool TryFill(FileDialogTarget target, string fullPath)
    {
        if (!target.IsValid || string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var path = fullPath.Trim();
        if (TrySetViaAutomation(target.FileNameEditHwnd, path))
        {
            return true;
        }

        if (TrySetViaAutomation(target.DialogHwnd, path))
        {
            return true;
        }

        return NativeMethods.SendMessage(target.FileNameEditHwnd, WmSetText, 0, path) != 0
            || NativeMethods.SetWindowText(target.FileNameEditHwnd, path);
    }

    private static bool TrySetViaAutomation(nint hwnd, string path)
    {
        if (hwnd == 0)
        {
            return false;
        }

        try
        {
            var element = AutomationElement.FromHandle(hwnd);
            if (element is null)
            {
                return false;
            }

            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj)
                && patternObj is ValuePattern value)
            {
                value.SetValue(path);
                return true;
            }

            // Dialog root: find the filename edit by control type / name heuristics.
            if (hwnd != 0)
            {
                var edit = FindFilenameEdit(element);
                if (edit is not null
                    && edit.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj)
                    && patternObj is ValuePattern nested)
                {
                    nested.SetValue(path);
                    return true;
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return false;
    }

    private static AutomationElement? FindFilenameEdit(AutomationElement root)
    {
        try
        {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsValuePatternAvailableProperty, true));
            var edits = root.FindAll(TreeScope.Descendants, condition);
            if (edits is null || edits.Count == 0)
            {
                return null;
            }

            // Prefer edit whose name mentions file name.
            foreach (AutomationElement edit in edits)
            {
                var name = edit.Current.Name ?? string.Empty;
                if (name.Contains("file name", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("文件名", StringComparison.Ordinal)
                    || name.Contains("檔案", StringComparison.Ordinal))
                {
                    return edit;
                }
            }

            return edits[edits.Count - 1];
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint SendMessage(nint hWnd, uint msg, nint wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetWindowText(nint hWnd, string lpString);
    }
}
