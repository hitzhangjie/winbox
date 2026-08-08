namespace WinBox.Host.Ui.DialogAssist;

/// <summary>Testable façade over Win32 window queries used by dialog detection.</summary>
public interface IWindowInspection
{
    nint GetForegroundWindow();

    bool IsWindow(nint hwnd);

    bool IsWindowVisible(nint hwnd);

    string GetClassName(nint hwnd);

    string GetWindowText(nint hwnd);

    bool TryGetWindowRect(nint hwnd, out int left, out int top, out int right, out int bottom);

    IReadOnlyList<nint> GetChildWindows(nint parent, bool recursive);
}
