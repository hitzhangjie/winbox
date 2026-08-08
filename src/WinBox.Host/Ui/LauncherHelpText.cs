namespace WinBox.Host.Ui;

/// <summary>
/// Shared launcher trigger / shortcut rows.
/// Help uses read-only guide copy; Settings → Shortcuts uses preferences-oriented copy
/// (editing of triggers and hotkeys is planned).
/// </summary>
public static class LauncherHelpText
{
    public const string WindowTitle = "WinBox Help";

    /// <summary>Tray Help — calm read-only reference.</summary>
    public const string HelpIntro =
        "Quick reference for the launcher. Prefixes switch modes; plain text searches indexed files.";

    public const string HelpModesHeading = "What you can type";
    public const string HelpKeysHeading = "Keyboard";
    public const string HelpTrayHeading = "Tray";

    /// <summary>Settings → Shortcuts — framed as defaults you will be able to change.</summary>
    public const string SettingsIntro =
        "Current defaults for input triggers and hotkeys. This tab will let you edit them; " +
        "Web keywords are already editable under Settings → Web.";

    public const string SettingsModesHeading = "Input triggers";
    public const string SettingsModesHint =
        "Soon: customize prefixes and keywords here. Until then, use Settings → Web for web aliases.";

    public const string SettingsKeysHeading = "Hotkeys";
    public const string SettingsKeysHint =
        "Soon: rebind summon and result actions. Values below are the shipped defaults.";

    public const string SettingsTrayHeading = "Tray actions";
    public const string SettingsTrayHint = "Tray clicks stay fixed for now.";

    public const string SettingsFooterHint =
        "Need a read-only walkthrough? Tray → Help. Editing for triggers and hotkeys is tracked for a later release.";

    public const string SettingsStatus = "Defaults shown · editing coming later";

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
