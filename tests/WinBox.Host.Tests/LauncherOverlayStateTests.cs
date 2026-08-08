using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LauncherOverlayStateTests
{
    [Fact]
    public void Activate_MakesVisibleAndClearsQuery()
    {
        var state = new LauncherOverlayState();
        state.SetQuery("partial");

        state.Activate();

        Assert.True(state.IsVisible);
        Assert.Equal(string.Empty, state.Query);
    }

    [Fact]
    public void Dismiss_HidesAndClearsQuery()
    {
        var state = new LauncherOverlayState();
        state.Activate();
        state.SetQuery("winbox");

        state.Dismiss();

        Assert.False(state.IsVisible);
        Assert.Equal(string.Empty, state.Query);
    }

    [Fact]
    public void Activate_WhenAlreadyVisible_ResetsQuery()
    {
        var state = new LauncherOverlayState();
        state.Activate();
        state.SetQuery("old");

        state.Activate();

        Assert.True(state.IsVisible);
        Assert.Equal(string.Empty, state.Query);
    }
}
