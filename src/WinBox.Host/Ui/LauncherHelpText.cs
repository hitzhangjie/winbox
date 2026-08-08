namespace WinBox.Host.Ui;

/// <summary>
/// Discoverability copy for launcher input modes and shortcuts.
/// Shared by tray Help and Settings → Shortcuts.
/// </summary>
public static class LauncherHelpText
{
    public const string WindowTitle = "WinBox Help";
    public const string Intro =
        "Summon the launcher, then type. Prefixes switch modes; plain text searches indexed files.";

    public static IReadOnlyList<(string Trigger, string Description)> QueryModes { get; } =
    [
        ("filename…", "Search indexed files and folders. Enter opens; Alt+Enter reveals in Explorer."),
        ("gg / so / yt / x …", "Web search (keyword + space + query). Edit aliases in Settings → Web."),
        ("1+2*3", "Calculator — Enter copies the result."),
        ("> command", "Run a command in cmd.exe."),
        ("? prompt", "Ask AI — Enter sends (streams). Enter again copies. Configure in Settings → AI."),
    ];

    public static IReadOnlyList<(string Keys, string Description)> Shortcuts { get; } =
    [
        ("Shift+Alt+U", "Open launcher"),
        ("Esc", "Dismiss launcher"),
        ("↑ / ↓", "Move selection"),
        ("Enter", "Activate selected result"),
        ("Alt+Enter", "Reveal path in Explorer"),
    ];

    public static IReadOnlyList<(string Keys, string Description)> TrayActions { get; } =
    [
        ("Double-click", "Open launcher"),
        ("Right-click", "Open Launcher / Settings / Help / Quit"),
    ];

    /// <summary>Flat lines for tests and console — trigger then description.</summary>
    public static IEnumerable<string> QueryModeLines()
    {
        foreach (var (trigger, description) in QueryModes)
        {
            yield return $"{trigger} — {description}";
        }
    }
}
