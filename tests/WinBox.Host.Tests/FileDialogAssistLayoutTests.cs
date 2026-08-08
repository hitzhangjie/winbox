using WinBox.Host.Ui.DialogAssist;

namespace WinBox.Host.Tests;

public sealed class FileDialogAssistLayoutTests
{
    [Fact]
    public void PlaceUnderDialog_CentersFixedWidthUnderWideDialog()
    {
        // Dialog 1000px wide at x=100; scale 1; preferred 520 → left = 100 + (1000-520)/2 = 340
        var (left, top, width) = FileDialogAssistLayout.PlaceUnderDialog(
            dialogLeftPx: 100,
            dialogBottomPx: 800,
            dialogWidthPx: 1000,
            dpiScale: 1);

        Assert.Equal(340, left);
        Assert.Equal(800, top);
        Assert.Equal(FileDialogAssistLayout.FixedWidthDip, width);
    }

    [Fact]
    public void PlaceUnderDialog_ShrinksWhenDialogNarrowerThanPreferred()
    {
        var (left, top, width) = FileDialogAssistLayout.PlaceUnderDialog(
            dialogLeftPx: 50,
            dialogBottomPx: 400,
            dialogWidthPx: 400,
            dpiScale: 1);

        Assert.Equal(50, left);
        Assert.Equal(400, top);
        Assert.Equal(400, width);
    }

    [Fact]
    public void PlaceUnderDialog_RespectsDpiScale()
    {
        var (left, top, width) = FileDialogAssistLayout.PlaceUnderDialog(
            dialogLeftPx: 200,
            dialogBottomPx: 900,
            dialogWidthPx: 1000,
            dpiScale: 2);

        Assert.Equal(100, left);
        Assert.Equal(450, top);
        Assert.Equal(500, width);
    }
}
