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
            });

            var loaded = store.LoadOrDefault();

            Assert.Equal(120, loaded.OverlayLeft);
            Assert.Equal(80, loaded.OverlayTop);
            Assert.Equal(640, loaded.OverlayWidth);
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
}
