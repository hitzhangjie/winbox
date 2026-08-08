using WinBox.Abstractions;
using WinBox.Host.Ui;
using WinBox.Host.Ui.DialogAssist;

namespace WinBox.Host.Tests;

public sealed class FileDialogDetectorTests
{
    [Fact]
    public void TryDetect_RejectsNonDialogClass()
    {
        var windows = new FakeWindowInspection();
        windows.Add(1, "CabinetWClass", "Explorer", visible: true, 0, 0, 800, 600);
        var detector = new FileDialogDetector(windows);

        Assert.False(detector.TryDetect(1, out _));
    }

    [Fact]
    public void TryDetect_AcceptsCommonOpenDialog()
    {
        var windows = BuildOpenDialog(dialog: 10, edit: 11, button: 12, label: 13);
        var detector = new FileDialogDetector(windows);

        Assert.True(detector.TryDetect(10, out var target));
        Assert.Equal(10, target.DialogHwnd);
        Assert.Equal(11, target.FileNameEditHwnd);
        Assert.True(target.IsValid);
    }

    [Fact]
    public void TryDetect_AcceptsChineseOpenDialog()
    {
        var windows = new FakeWindowInspection();
        windows.Add(20, FileDialogDetector.CommonDialogClass, "打开", visible: true, 100, 100, 900, 700);
        windows.Add(21, "Static", "文件名(&N):", visible: true, 120, 620, 200, 640, parent: 20);
        windows.Add(22, "ComboBoxEx32", "", visible: true, 210, 618, 700, 642, parent: 20);
        windows.Add(23, "Edit", "", visible: true, 214, 620, 696, 640, parent: 22);
        windows.Add(24, "Button", "打开(&O)", visible: true, 620, 650, 700, 680, parent: 20);
        windows.SetChildren(20, [21, 22, 23, 24]);

        var detector = new FileDialogDetector(windows);

        Assert.True(detector.TryDetect(20, out var target));
        Assert.Equal(23, target.FileNameEditHwnd);
    }

    [Fact]
    public void TryDetect_RejectsDialogWithoutConfirmButton()
    {
        var windows = new FakeWindowInspection();
        windows.Add(30, FileDialogDetector.CommonDialogClass, "About", visible: true, 0, 0, 400, 300);
        windows.Add(31, "Edit", "", visible: true, 40, 200, 300, 220, parent: 30);
        windows.Add(32, "Button", "OK", visible: true, 200, 240, 280, 270, parent: 30);
        windows.SetChildren(30, [31, 32]);

        var detector = new FileDialogDetector(windows);

        Assert.False(detector.TryDetect(30, out _));
    }

    private static FakeWindowInspection BuildOpenDialog(nint dialog, nint edit, nint button, nint label)
    {
        var windows = new FakeWindowInspection();
        windows.Add(dialog, FileDialogDetector.CommonDialogClass, "Open", visible: true, 50, 50, 850, 650);
        windows.Add(label, "Static", "File &name:", visible: true, 70, 560, 150, 580, parent: dialog);
        windows.Add(100, "ComboBoxEx32", "", visible: true, 160, 558, 700, 582, parent: dialog);
        windows.Add(edit, "Edit", "", visible: true, 164, 560, 696, 580, parent: 100);
        windows.Add(button, "Button", "&Open", visible: true, 620, 600, 700, 630, parent: dialog);
        windows.SetChildren(dialog, [label, 100, edit, button]);
        return windows;
    }
}

public sealed class FileDialogAssistSessionTests
{
    [Fact]
    public void SetResults_SelectsFirstHit()
    {
        var session = new FileDialogAssistSession();
        session.SetResults(
        [
            new SearchHit(@"C:\a.pdf", "a.pdf", 1),
            new SearchHit(@"C:\b.pdf", "b.pdf", 0.5),
        ]);

        Assert.Equal(0, session.SelectedIndex);
        Assert.Equal("a.pdf", session.SelectedHit?.Name);
    }

    [Fact]
    public void MoveSelection_ClampsAndUpdates()
    {
        var session = new FileDialogAssistSession();
        session.SetResults(
        [
            new SearchHit(@"C:\a.txt", "a.txt", 1),
            new SearchHit(@"C:\b.txt", "b.txt", 0.8),
            new SearchHit(@"C:\c.txt", "c.txt", 0.6),
        ]);

        Assert.True(session.MoveSelection(1));
        Assert.Equal(1, session.SelectedIndex);
        Assert.True(session.MoveSelection(5));
        Assert.Equal(2, session.SelectedIndex);
        Assert.False(session.MoveSelection(1));
        Assert.True(session.MoveSelection(-1));
        Assert.Equal(1, session.SelectedIndex);
    }

    [Fact]
    public void ClearResults_ResetsSelection()
    {
        var session = new FileDialogAssistSession();
        session.SetQuery("pdf");
        session.SetResults([new SearchHit(@"C:\x.pdf", "x.pdf", 1)]);
        session.ClearResults();

        Assert.Empty(session.Results);
        Assert.Equal(-1, session.SelectedIndex);
        Assert.Null(session.SelectedHit);
        Assert.Equal("pdf", session.Query);
    }
}

public sealed class FileDialogPathFillerContractTests
{
    [Fact]
    public void RecordingFiller_CapturesFillRequest()
    {
        var filler = new RecordingPathFiller();
        var target = new FileDialogTarget(1, 2, 0, 0, 100, 100);

        Assert.True(filler.TryFill(target, @"D:\docs\report.pdf"));
        Assert.Equal(target, filler.LastTarget);
        Assert.Equal(@"D:\docs\report.pdf", filler.LastPath);
    }

    [Fact]
    public void RecordingFiller_RejectsBlankPath()
    {
        var filler = new RecordingPathFiller();
        var target = new FileDialogTarget(1, 2, 0, 0, 100, 100);

        Assert.False(filler.TryFill(target, "  "));
        Assert.Null(filler.LastPath);
    }

    private sealed class RecordingPathFiller : IFileDialogPathFiller
    {
        public FileDialogTarget? LastTarget { get; private set; }

        public string? LastPath { get; private set; }

        public bool TryFill(FileDialogTarget target, string fullPath)
        {
            if (!target.IsValid || string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            LastTarget = target;
            LastPath = fullPath.Trim();
            return true;
        }
    }
}

public sealed class UiOptionsFileDialogAssistTests
{
    [Fact]
    public void Normalize_PreservesFileDialogAssistEnabled()
    {
        var normalized = UiOptionsStore.Normalize(new UiOptions
        {
            FileDialogAssistEnabled = false,
        });

        Assert.False(normalized.FileDialogAssistEnabled);
    }

    [Fact]
    public void Save_Then_Load_RoundTripsFileDialogAssistEnabled()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-ui-dialog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new UiOptionsStore(path);
            store.Save(new UiOptions { FileDialogAssistEnabled = false });

            var loaded = store.LoadOrDefault();
            Assert.False(loaded.FileDialogAssistEnabled);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void Default_FileDialogAssistEnabled_IsTrue()
    {
        Assert.True(new UiOptions().FileDialogAssistEnabled);
        Assert.True(UiOptionsStore.Normalize(new UiOptions()).FileDialogAssistEnabled);
    }
}

/// <summary>In-memory window tree for <see cref="FileDialogDetector"/> tests.</summary>
internal sealed class FakeWindowInspection : IWindowInspection
{
    private readonly Dictionary<nint, WindowInfo> _windows = new();
    private readonly Dictionary<nint, List<nint>> _children = new();
    private nint _foreground;

    public void Add(
        nint hwnd,
        string className,
        string text,
        bool visible,
        int left,
        int top,
        int right,
        int bottom,
        nint parent = 0)
    {
        _windows[hwnd] = new WindowInfo(className, text, visible, left, top, right, bottom, parent);
        if (parent != 0)
        {
            if (!_children.TryGetValue(parent, out var list))
            {
                list = [];
                _children[parent] = list;
            }

            if (!list.Contains(hwnd))
            {
                list.Add(hwnd);
            }
        }
    }

    public void SetChildren(nint parent, IEnumerable<nint> children) =>
        _children[parent] = children.ToList();

    public void SetForeground(nint hwnd) => _foreground = hwnd;

    public nint GetForegroundWindow() => _foreground;

    public bool IsWindow(nint hwnd) => _windows.ContainsKey(hwnd);

    public bool IsWindowVisible(nint hwnd) =>
        _windows.TryGetValue(hwnd, out var info) && info.Visible;

    public string GetClassName(nint hwnd) =>
        _windows.TryGetValue(hwnd, out var info) ? info.ClassName : string.Empty;

    public string GetWindowText(nint hwnd) =>
        _windows.TryGetValue(hwnd, out var info) ? info.Text : string.Empty;

    public bool TryGetWindowRect(nint hwnd, out int left, out int top, out int right, out int bottom)
    {
        if (_windows.TryGetValue(hwnd, out var info))
        {
            left = info.Left;
            top = info.Top;
            right = info.Right;
            bottom = info.Bottom;
            return true;
        }

        left = top = right = bottom = 0;
        return false;
    }

    public IReadOnlyList<nint> GetChildWindows(nint parent, bool recursive)
    {
        if (!_children.TryGetValue(parent, out var direct))
        {
            return Array.Empty<nint>();
        }

        if (!recursive)
        {
            return direct;
        }

        var all = new List<nint>();
        void Walk(nint node)
        {
            if (!_children.TryGetValue(node, out var kids))
            {
                return;
            }

            foreach (var kid in kids)
            {
                all.Add(kid);
                Walk(kid);
            }
        }

        Walk(parent);
        return all;
    }

    private sealed record WindowInfo(
        string ClassName,
        string Text,
        bool Visible,
        int Left,
        int Top,
        int Right,
        int Bottom,
        nint Parent);
}
