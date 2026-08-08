using System.Windows.Controls;

namespace WinBox.Host.Ui;

/// <summary>
/// Results pane scroll chrome shared by the launcher overlay: pixel scrolling + themed bar
/// (same grammar as Settings), so tall AI rows can scroll instead of clipping.
/// </summary>
internal static class LauncherResultsScroll
{
    public static void Configure(ListBox results)
    {
        ArgumentNullException.ThrowIfNull(results);

        ScrollViewer.SetHorizontalScrollBarVisibility(results, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(results, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
        ScrollViewer.SetCanContentScroll(results, false);
        VirtualizingPanel.SetIsVirtualizing(results, false);
        VirtualizingPanel.SetScrollUnit(results, ScrollUnit.Pixel);
        ThemedScrollBars.Apply(results);
    }
}
