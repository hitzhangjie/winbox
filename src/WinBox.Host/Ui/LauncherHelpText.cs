namespace WinBox.Host.Ui;

/// <summary>
/// Shared launcher trigger / shortcut rows.
/// Help uses read-only guide copy; Settings → Shortcuts lets you rebind the summon hotkey.
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

    /// <summary>Settings → Shortcuts — summon hotkey is editable; other rows are fixed for now.</summary>
    public const string SettingsIntro =
        "Rebind the global hotkey that opens the launcher. Input triggers and in-launcher keys " +
        "stay fixed for now; Web keywords are editable under Settings → Web.";

    public const string SettingsModesHeading = "Input triggers";
    public const string SettingsModesHint =
        "Soon: customize prefixes and keywords here. Until then, use Settings → Web for web aliases.";

    public const string SettingsKeysHeading = "Hotkeys";
    public const string SettingsKeysHint =
        "Capture… records a new combination (needs Ctrl, Alt, Shift, or Win). Esc cancels.";

    /// <summary>Accent line under Open launcher — default must stay visually obvious.</summary>
    public static string SettingsDefaultHotkeyBadge =>
        $"Default  {LauncherHotkeyBinding.DefaultDisplay}";

    public const string SettingsTrayHeading = "Tray actions";
    public const string SettingsTrayHint = "Tray clicks stay fixed for now.";

    public const string SettingsFooterHint =
        "Need a read-only walkthrough? Tray → Help. Other in-launcher shortcuts stay fixed for now.";

    public const string SettingsStatus = "Open launcher editable · other keys fixed";

    public const string OpenLauncherDescription = "Open launcher";

    public static IReadOnlyList<(string Trigger, string Description)> QueryModes { get; } =
    [
        ("filename…", "Search indexed files and folders. Enter opens; Alt+Enter reveals in Explorer."),
        ("gg / so / yt / x …", "Web search (keyword + space + query). Edit aliases in Settings → Web."),
        ("1+2*3", "Calculator — Enter copies the result."),
        ("> command", "Run a command in cmd.exe."),
        ("? prompt", "Ask AI — Enter sends (streams). Enter again copies. Configure in Settings → AI."),
    ];

    /// <summary>In-launcher keys only (no global summon) — shown under the editable field in Settings.</summary>
    public static IReadOnlyList<(string Keys, string Description)> InLauncherShortcuts { get; } =
    [
        ("Esc", "Dismiss launcher"),
        ("↑ / ↓", "Move selection"),
        ("Enter", "Activate selected result"),
        ("Alt+Enter", "Reveal path in Explorer"),
    ];

    /// <summary>Shipped defaults (open launcher uses <see cref="LauncherHotkeyBinding.DefaultDisplay"/>).</summary>
    public static IReadOnlyList<(string Keys, string Description)> Shortcuts { get; } =
        BuildShortcuts(LauncherHotkeyBinding.DefaultDisplay);

    public static IReadOnlyList<(string Keys, string Description)> TrayActions { get; } =
    [
        ("Double-click", "Open launcher"),
        ("Right-click", "Open Launcher / Settings / Help / Quit"),
    ];

    public static IReadOnlyList<(string Keys, string Description)> ShortcutsWith(string? openLauncherKeys) =>
        BuildShortcuts(LauncherHotkeyBinding.Normalize(openLauncherKeys));

    /// <summary>Flat lines for tests and console — trigger then description.</summary>
    public static IEnumerable<string> QueryModeLines()
    {
        foreach (var (trigger, description) in QueryModes)
        {
            yield return $"{trigger} — {description}";
        }
    }

    private static IReadOnlyList<(string Keys, string Description)> BuildShortcuts(string openLauncherKeys) =>
    [
        (openLauncherKeys, OpenLauncherDescription),
        .. InLauncherShortcuts,
    ];
}
