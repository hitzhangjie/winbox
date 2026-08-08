using WinBox.Abstractions;
using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class PathActivationShortcutsTests
{
    [Fact]
    public void Resolve_Alt_OnOpenPath_RevealsFolder()
    {
        var action = PathActivationShortcuts.ResolveOpenPathOverride(
            ResultActionKind.OpenPath,
            alt: true);

        Assert.Equal(ResultActionKind.OpenContainingFolder, action);
    }

    [Fact]
    public void Resolve_EnterAlone_NoOverride()
    {
        var action = PathActivationShortcuts.ResolveOpenPathOverride(
            ResultActionKind.OpenPath,
            alt: false);

        Assert.Null(action);
    }

    [Fact]
    public void Resolve_NonOpenPath_NoOverride()
    {
        var action = PathActivationShortcuts.ResolveOpenPathOverride(
            ResultActionKind.OpenUrl,
            alt: true);

        Assert.Null(action);
    }
}
