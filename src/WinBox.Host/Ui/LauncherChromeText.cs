using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>Calm launcher microcopy — keep strings centralized for tests and consistency.</summary>
public static class LauncherChromeText
{
    public const string IdleHint = "Type to search files and paths";
    public const string IdleDetail = "↑↓ select  ·  Enter open  ·  Esc close";
    public const string NoResultsDetail = "Try a shorter name, or check Index roots in Settings";
    public const string Placeholder = "Search…";

    public static string NoResultsTitle(string? query)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "No results";
        }

        if (trimmed.Length > 48)
        {
            trimmed = trimmed[..45] + "…";
        }

        return $"No results for “{trimmed}”";
    }

    public static string FooterFor(ResultActionKind? selectedAction, bool hasResults)
    {
        if (!hasResults || selectedAction is null or ResultActionKind.None)
        {
            return "Esc close  ·  drag to move";
        }

        if (selectedAction == ResultActionKind.Submit)
        {
            return "Enter ask AI  ·  Esc close";
        }

        return selectedAction is ResultActionKind.OpenPath or ResultActionKind.OpenContainingFolder
            ? "Enter open  ·  Alt+Enter reveal  ·  Esc close"
            : "Enter activate  ·  Esc close";
    }
}
