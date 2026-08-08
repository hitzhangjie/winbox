using System.Windows.Input;
using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LauncherHotkeyBindingTests
{
    [Fact]
    public void DefaultDisplay_IsAltU()
    {
        Assert.Equal("Alt+U", LauncherHotkeyBinding.DefaultDisplay);
        Assert.Equal(ModifierKeys.Alt, LauncherHotkeyBinding.DefaultModifiers);
        Assert.Equal(Key.U, LauncherHotkeyBinding.DefaultKey);
        Assert.Equal("Alt+U", new UiOptions().LauncherHotkey);
    }

    [Theory]
    [InlineData("Alt+U", ModifierKeys.Alt, Key.U)]
    [InlineData("alt+u", ModifierKeys.Alt, Key.U)]
    [InlineData("Ctrl+Shift+Space", ModifierKeys.Control | ModifierKeys.Shift, Key.Space)]
    [InlineData("Shift+Alt+U", ModifierKeys.Alt | ModifierKeys.Shift, Key.U)]
    [InlineData("Win+F12", ModifierKeys.Windows, Key.F12)]
    public void TryParse_AcceptsCommonGestures(string text, ModifierKeys modifiers, Key key)
    {
        Assert.True(LauncherHotkeyBinding.TryParse(text, out var parsedMods, out var parsedKey));
        Assert.Equal(modifiers, parsedMods);
        Assert.Equal(key, parsedKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("U")]
    [InlineData("Alt")]
    [InlineData("Alt+Escape")]
    [InlineData("Ctrl+Alt")]
    public void TryParse_RejectsInvalid(string? text)
    {
        Assert.False(LauncherHotkeyBinding.TryParse(text, out _, out _));
    }

    [Fact]
    public void Format_OrdersModifiersConsistently()
    {
        Assert.Equal(
            "Ctrl+Alt+Shift+U",
            LauncherHotkeyBinding.Format(
                ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Control,
                Key.U));
    }

    [Fact]
    public void Normalize_FallsBackToDefault()
    {
        Assert.Equal(LauncherHotkeyBinding.DefaultDisplay, LauncherHotkeyBinding.Normalize("nope"));
        Assert.Equal("Alt+U", LauncherHotkeyBinding.Normalize("alt + u"));
    }
}

public sealed class UiOptionsLauncherHotkeyTests
{
    [Fact]
    public void Normalize_PreservesValidLauncherHotkey()
    {
        var normalized = UiOptionsStore.Normalize(new UiOptions
        {
            LauncherHotkey = "Ctrl+Alt+K",
        });

        Assert.Equal("Ctrl+Alt+K", normalized.LauncherHotkey);
    }

    [Fact]
    public void Normalize_ReplacesInvalidLauncherHotkey()
    {
        var normalized = UiOptionsStore.Normalize(new UiOptions
        {
            LauncherHotkey = "U",
        });

        Assert.Equal(LauncherHotkeyBinding.DefaultDisplay, normalized.LauncherHotkey);
    }

    [Fact]
    public void Save_Then_Load_RoundTripsLauncherHotkey()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-ui-hotkey-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new UiOptionsStore(path);
            store.Save(new UiOptions { LauncherHotkey = "Ctrl+Shift+Space" });

            var loaded = store.LoadOrDefault();
            Assert.Equal("Ctrl+Shift+Space", loaded.LauncherHotkey);
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
