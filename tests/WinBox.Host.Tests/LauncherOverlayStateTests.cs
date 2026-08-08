using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LauncherOverlayStateTests
{
    [Fact]
    public void Activate_MakesVisibleAndClearsQuery()
    {
        var state = new LauncherOverlayState();
        state.SetRawQuery("partial");

        state.Activate();

        Assert.True(state.IsVisible);
        Assert.Equal(string.Empty, state.Query);
        Assert.Null(state.ModeLabel);
        Assert.Empty(state.Results);
    }

    [Fact]
    public void Dismiss_HidesAndClearsQuery()
    {
        var state = new LauncherOverlayState();
        state.Activate();
        state.SetRawQuery("winbox");

        state.Dismiss();

        Assert.False(state.IsVisible);
        Assert.Equal(string.Empty, state.Query);
    }

    [Fact]
    public void ComposeRawFromPayload_UsesPrefix()
    {
        var state = new LauncherOverlayState();
        state.ApplyMatch(
            new Abstractions.QueryMatch("winbox.web", 80, "google ", "hi", "Google"),
            "google hi");

        var raw = state.ComposeRawFromPayload("maps");

        Assert.Equal("google maps", raw);
        Assert.Equal("Google", state.ModeLabel);
    }
}
