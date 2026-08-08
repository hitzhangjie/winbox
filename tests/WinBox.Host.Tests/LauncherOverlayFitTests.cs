using System.Windows;
using System.Windows.Media;
using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LauncherOverlayFitTests
{
    [Fact]
    public void ClampContentWidth_NeverBelowBase_OrAboveOnePointFive()
    {
        Assert.Equal(600, LauncherOverlayFit.ClampContentWidth(600, 100));
        Assert.Equal(600, LauncherOverlayFit.ClampContentWidth(600, 600));
        Assert.Equal(900, LauncherOverlayFit.ClampContentWidth(600, 1000));
        Assert.Equal(750, LauncherOverlayFit.ClampContentWidth(600, 750));
    }

    [Fact]
    public void IsNearBottom_TrueWhenAtOrNearEnd()
    {
        Assert.True(LauncherOverlayFit.IsNearBottom(0, 200, 180)); // no overflow
        Assert.True(LauncherOverlayFit.IsNearBottom(100, 200, 300)); // scrollable=100, at bottom
        Assert.True(LauncherOverlayFit.IsNearBottom(90, 200, 300, tolerancePx: 20));
        Assert.False(LauncherOverlayFit.IsNearBottom(20, 200, 400));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldFollowStream_RequiresPinAndStreaming(
        bool pinned,
        bool streaming,
        bool expected)
    {
        Assert.Equal(expected, LauncherOverlayFit.ShouldFollowStream(pinned, streaming));
    }

    [Fact]
    public void EstimatePreferredContentWidth_GrowsWithLongLine_AndAddsChrome()
    {
        Exception? error = null;
        double shortW = 0;
        double longW = 0;
        var thread = new Thread(() =>
        {
            try
            {
                shortW = LauncherOverlayFit.EstimatePreferredContentWidth(
                    "hi",
                    15,
                    new FontFamily("Segoe UI"));
                longW = LauncherOverlayFit.EstimatePreferredContentWidth(
                    new string('W', 80),
                    15,
                    new FontFamily("Segoe UI"));
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

        Assert.True(longW > shortW);
        Assert.True(shortW >= LauncherOverlayFit.ContentChromePadding);
        var clamped = LauncherOverlayFit.ClampContentWidth(600, longW);
        Assert.InRange(clamped, 600, 600 * LauncherOverlayFit.MaxWidthFactor);
    }
}
