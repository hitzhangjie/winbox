using System.Windows.Controls;
using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LauncherResultsScrollTests
{
    [Fact]
    public void ResultsList_UsesPixelScroll_NotItemScroll()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var list = new ListBox { MaxHeight = 200 };
                LauncherResultsScroll.Configure(list);

                Assert.False(ScrollViewer.GetCanContentScroll(list));
                Assert.False(VirtualizingPanel.GetIsVirtualizing(list));
                Assert.Equal(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(list));
                Assert.Equal(
                    ScrollBarVisibility.Disabled,
                    ScrollViewer.GetHorizontalScrollBarVisibility(list));
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }
    }
}
