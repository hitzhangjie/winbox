using System.Windows.Controls;
using System.Drawing;
using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class UiThemeTests
{
    [Fact]
    public void ParseTheme_AcceptsKnownValues()
    {
        Assert.Equal(UiThemeKind.Dark, WinBoxTheme.ParseTheme("dark"));
        Assert.Equal(UiThemeKind.Light, WinBoxTheme.ParseTheme("Light"));
        Assert.Equal(UiThemeKind.System, WinBoxTheme.ParseTheme("system"));
        Assert.Equal(UiThemeKind.Dark, WinBoxTheme.ParseTheme("nope"));
    }

    [Fact]
    public void Apply_Light_UpdatesPrimaryTextTowardDarkInk()
    {
        var previous = WinBoxTheme.CurrentKind;
        try
        {
            WinBoxTheme.Apply(UiThemeKind.Light);
            Assert.Equal(UiThemeKind.Light, WinBoxTheme.CurrentKind);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R < 0x40);

            WinBoxTheme.Apply(UiThemeKind.Dark);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R > 0xC0);
        }
        finally
        {
            WinBoxTheme.Apply(previous);
        }
    }

    [Fact]
    public void Normalize_ClampsAppearanceFields()
    {
        var normalized = UiOptionsStore.Normalize(new UiOptions
        {
            OverlayWidth = 50,
            ResultsMaxHeight = 9999,
            FontInput = 3,
            ScrollBarWidth = 100,
            ScrollBarMode = "ALWAYS",
            Theme = "light",
        });

        Assert.Equal(WinBoxTheme.OverlayWidth, normalized.OverlayWidth);
        Assert.Equal(WinBoxTheme.ResultsMaxHeight, normalized.ResultsMaxHeight);
        Assert.Equal(WinBoxTheme.FontInput, normalized.FontInput);
        Assert.Equal(8, normalized.ScrollBarWidth);
        Assert.Equal("always", normalized.ScrollBarMode);
        Assert.Equal("light", normalized.Theme);
    }
}

public sealed class UiLayoutTests
{
    [Fact]
    public void Apply_UpdatesLiveLayout()
    {
        UiLayout.Apply(new UiOptions
        {
            OverlayWidth = 720,
            ResultsMaxHeight = 400,
            FontInput = 20,
            FontTitle = 16,
            FontSubtitle = 13,
            ScrollBarWidth = 10,
            ScrollBarMode = "hidden",
            Theme = "dark",
        });

        Assert.Equal(720, UiLayout.OverlayWidth);
        Assert.Equal(400, UiLayout.ResultsMaxHeight);
        Assert.Equal(20, UiLayout.FontInput);
        Assert.Equal(ScrollBarShowMode.Hidden, UiLayout.ScrollBarMode);
        Assert.Equal(ScrollBarVisibility.Hidden, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
    }
}

public sealed class TrayIconFactoryTests
{
    [Fact]
    public void CreateGenerated_ReturnsUsableIcon()
    {
        using var icon = TrayIconFactory.CreateGenerated();
        Assert.True(icon.Width >= 16);
        Assert.True(icon.Height >= 16);
    }

    [Fact]
    public void WriteToFile_CreatesIcoOnDisk()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-tray-" + Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            TrayIconFactory.WriteToFile(path);
            Assert.True(System.IO.File.Exists(path));
            Assert.True(new System.IO.FileInfo(path).Length > 100);
            using var icon = new Icon(path);
            Assert.True(icon.Width >= 16);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
