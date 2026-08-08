using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class UiOptionsStoreTests
{
    [Fact]
    public void Save_Then_Load_RoundTripsPosition()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-ui-options-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new UiOptionsStore(path);
            store.Save(new UiOptions
            {
                OverlayLeft = 120,
                OverlayTop = 80,
                OverlayWidth = 640,
                Theme = "light",
            });

            var loaded = store.LoadOrDefault();

            Assert.Equal(120, loaded.OverlayLeft);
            Assert.Equal(80, loaded.OverlayTop);
            Assert.Equal(640, loaded.OverlayWidth);
            Assert.Equal("light", loaded.Theme);
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
    public void Normalize_ReplacesInvalidWidth()
    {
        var normalized = UiOptionsStore.Normalize(new UiOptions
        {
            OverlayWidth = 50,
            OverlayLeft = double.NaN,
        });

        Assert.Equal(WinBoxTheme.OverlayWidth, normalized.OverlayWidth);
        Assert.Null(normalized.OverlayLeft);
    }

    [Fact]
    public void LoadOrDefault_MissingFile_ReturnsDefaults()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-ui-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new UiOptionsStore(path);

        var loaded = store.LoadOrDefault();

        Assert.Null(loaded.OverlayLeft);
        Assert.Null(loaded.OverlayTop);
        Assert.Equal(WinBoxTheme.OverlayWidth, loaded.OverlayWidth);
    }
}

public sealed class ResultRowModelTests
{
    [Fact]
    public void FromResult_MapsTitleSubtitleAndAction()
    {
        var item = new Abstractions.QueryResultItem(
            "id",
            "readme.md",
            @"D:\Github\winbox\README.md",
            @"D:\Github\winbox\README.md",
            Abstractions.ResultActionKind.OpenPath);

        var row = ResultRowModel.FromResult(item);

        Assert.Equal("readme.md", row.Title);
        Assert.Equal(@"D:\Github\winbox\README.md", row.Subtitle);
        Assert.Equal(Abstractions.ResultActionKind.OpenPath, row.Action);
        Assert.False(string.IsNullOrEmpty(row.Glyph));
    }

    [Fact]
    public void FromResult_OmitsBlankSubtitle()
    {
        var item = new Abstractions.QueryResultItem("id", "Only title");

        var row = ResultRowModel.FromResult(item);

        Assert.Null(row.Subtitle);
        Assert.Equal("Only title", row.ToolTipText);
    }

    [Fact]
    public void FromResult_UsesIconKeyGlyphOverActionFallback()
    {
        var item = new Abstractions.QueryResultItem(
            "id",
            "shot.png",
            @"D:\pics\shot.png",
            @"D:\pics\shot.png",
            Abstractions.ResultActionKind.OpenPath,
            IconKey: Abstractions.ResultIconKeys.Image);

        var row = ResultRowModel.FromResult(item);

        Assert.Equal(Abstractions.ResultIconKeys.Image, row.IconKey);
        Assert.Equal(WinBoxTheme.GlyphForIconKey(Abstractions.ResultIconKeys.Image), row.Glyph);
        Assert.NotEqual(WinBoxTheme.GlyphForAction(Abstractions.ResultActionKind.OpenPath), row.Glyph);
    }

    [Fact]
    public void FromResult_FallsBackToActionGlyphWhenIconKeyMissing()
    {
        var item = new Abstractions.QueryResultItem(
            "id",
            "title",
            Action: Abstractions.ResultActionKind.CopyText);

        var row = ResultRowModel.FromResult(item);

        Assert.Null(row.IconKey);
        Assert.Null(row.IconImage);
        Assert.Equal(WinBoxTheme.GlyphForAction(Abstractions.ResultActionKind.CopyText), row.Glyph);
    }

    [Fact]
    public void FromResult_PathItem_ResolvesShellIconImage()
    {
        var item = new Abstractions.QueryResultItem(
            "id",
            "notes.md",
            @"D:\docs\notes.md",
            @"D:\docs\notes.md",
            Abstractions.ResultActionKind.OpenPath,
            IconKey: Abstractions.ResultIconKeys.Markdown);

        var row = ResultRowModel.FromResult(item);

        Assert.NotNull(row.IconImage);
        Assert.True(row.IconImage.CanFreeze);
    }

    [Fact]
    public void FromResult_NonPathAction_SkipsShellIcon()
    {
        var item = new Abstractions.QueryResultItem(
            "calc",
            "42",
            Subtitle: "6*7",
            Payload: "42",
            Action: Abstractions.ResultActionKind.CopyText,
            IconKey: Abstractions.ResultIconKeys.Calculator);

        var row = ResultRowModel.FromResult(item);

        Assert.Null(row.IconImage);
        Assert.Equal(WinBoxTheme.GlyphForIconKey(Abstractions.ResultIconKeys.Calculator), row.Glyph);
    }
}

public sealed class ShellFileIconsTests
{
    [Fact]
    public void GetForPath_ReturnsFrozenIconForKnownExtension()
    {
        ShellFileIcons.ClearCache();

        var pdf = ShellFileIcons.GetForPath(@"C:\temp\report.pdf");
        var png = ShellFileIcons.GetForPath(@"D:\shots\a.png");

        Assert.NotNull(pdf);
        Assert.NotNull(png);
        Assert.True(pdf.IsFrozen);
        Assert.True(png.IsFrozen);
        Assert.NotSame(pdf, png);
    }

    [Fact]
    public void GetForPath_CachesByExtension()
    {
        ShellFileIcons.ClearCache();

        var first = ShellFileIcons.GetForPath(@"C:\a\one.md");
        var second = ShellFileIcons.GetForPath(@"D:\b\two.md");

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetForPath_RejectsNonRootedAndBlank()
    {
        Assert.Null(ShellFileIcons.GetForPath(null));
        Assert.Null(ShellFileIcons.GetForPath("   "));
        Assert.Null(ShellFileIcons.GetForPath("readme.md"));
        Assert.Null(ShellFileIcons.GetForPath("https://example.com/a.pdf"));
    }
}
