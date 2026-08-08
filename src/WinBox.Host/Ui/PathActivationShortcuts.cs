using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>
/// Maps modifier keys to path activation overrides (Spotlight-style reveal).
/// </summary>
public static class PathActivationShortcuts
{
    /// <summary>
    /// Alt on an <see cref="ResultActionKind.OpenPath"/> item reveals the containing folder.
    /// </summary>
    public static ResultActionKind? ResolveOpenPathOverride(
        ResultActionKind currentAction,
        bool alt)
    {
        if (currentAction != ResultActionKind.OpenPath)
        {
            return null;
        }

        return alt ? ResultActionKind.OpenContainingFolder : null;
    }
}
