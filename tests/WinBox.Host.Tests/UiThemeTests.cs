using System.Windows.Controls;
using System.Drawing;
using WinBox.Abstractions;
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
    public void Apply_ReplacesBrushInstances_AndUpdatesTextPrimaryLuma()
    {
        var previous = WinBoxTheme.CurrentKind;
        var before = WinBoxTheme.TextPrimaryBrush;
        try
        {
            WinBoxTheme.Apply(UiThemeKind.Light);
            Assert.NotSame(before, WinBoxTheme.TextPrimaryBrush);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R < 0x40);

            var lightBrush = WinBoxTheme.TextPrimaryBrush;
            WinBoxTheme.Apply(UiThemeKind.Dark);
            Assert.NotSame(lightBrush, WinBoxTheme.TextPrimaryBrush);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R > 0xC0);
        }
        finally
        {
            WinBoxTheme.Apply(previous);
        }
    }

    [Fact]
    public void Apply_Light_UpdatesPrimaryTextTowardDarkInk()
    {
        var previous = WinBoxTheme.CurrentKind;
        try
        {
            WinBoxTheme.Apply(UiThemeKind.Light);
            Assert.Equal(UiThemeKind.Light, WinBoxTheme.CurrentKind);
            Assert.False(WinBoxTheme.IsDarkEffective);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R < 0x40);
            Assert.True(WinBoxTheme.HoverBrush.Color.R > 0xE0);
            Assert.Equal(0xFF, WinBoxTheme.TextOnAccentBrush.Color.R);

            WinBoxTheme.Apply(UiThemeKind.Dark);
            Assert.True(WinBoxTheme.IsDarkEffective);
            Assert.True(WinBoxTheme.TextPrimaryBrush.Color.R > 0xC0);
            Assert.True(WinBoxTheme.HoverBrush.Color.R < 0x40);
        }
        finally
        {
            WinBoxTheme.Apply(previous);
        }
    }

    [Fact]
    public void Resolve_LightAndDark_KeepReadableContrastPairs()
    {
        var dark = WinBoxTheme.Resolve(UiThemeKind.Dark);
        var light = WinBoxTheme.Resolve(UiThemeKind.Light);

        Assert.True(Luma(dark.TextPrimary) > Luma(dark.SurfaceOverlay) + 0.4);
        Assert.True(Luma(light.SurfaceOverlay) > Luma(light.TextPrimary) + 0.4);
        Assert.NotEqual(dark.Hover, dark.Selection);
        Assert.NotEqual(light.Hover, light.Selection);
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

    [Fact]
    public void SettingsSpacingTokens_StayOnFourPxRhythm()
    {
        Assert.Equal(0, WinBoxTheme.SettingsPageMargin % 4);
        Assert.Equal(0, WinBoxTheme.SettingsContentPadding % 4);
        Assert.Equal(0, WinBoxTheme.SettingsContentPaddingRight % 2);
        Assert.True(WinBoxTheme.SettingsContentPaddingRight < WinBoxTheme.SettingsContentPadding);
        Assert.Equal(0, WinBoxTheme.SettingsSectionGap % 2);
        Assert.True(WinBoxTheme.SettingsCardRadius >= WinBoxTheme.ControlRadius);
    }

    private static double Luma(System.Windows.Media.Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}

public sealed class LauncherChromeTextTests
{
    [Fact]
    public void NoResultsTitle_IncludesQuery()
    {
        Assert.Equal("No results for “readme”", LauncherChromeText.NoResultsTitle("readme"));
        Assert.Equal("No results", LauncherChromeText.NoResultsTitle("  "));
    }

    [Fact]
    public void NoResultsTitle_TruncatesVeryLongQuery()
    {
        var longQuery = new string('a', 60);
        var title = LauncherChromeText.NoResultsTitle(longQuery);
        Assert.StartsWith("No results for “", title);
        Assert.Contains('…', title);
        Assert.True(title.Length < 80);
    }

    [Theory]
    [InlineData(ResultActionKind.OpenPath, true, "Alt+Enter")]
    [InlineData(ResultActionKind.CopyText, true, "Enter activate")]
    [InlineData(ResultActionKind.None, false, "Esc close")]
    public void FooterFor_MatchesSelectionContext(ResultActionKind action, bool hasResults, string expectedFragment)
    {
        var footer = LauncherChromeText.FooterFor(hasResults ? action : null, hasResults);
        Assert.Contains(expectedFragment, footer);
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
