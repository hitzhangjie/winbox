using System.Globalization;
using System.Text;
using System.Windows.Input;

namespace WinBox.Host.Ui;

/// <summary>
/// Parse / format / validate the global "open launcher" hotkey stored in <see cref="UiOptions"/>.
/// Display form matches Help / Settings: e.g. <c>Alt+U</c>, <c>Ctrl+Shift+Space</c>.
/// </summary>
public static class LauncherHotkeyBinding
{
    public const string DefaultDisplay = "Alt+U";

    public static readonly ModifierKeys DefaultModifiers = ModifierKeys.Alt;
    public static readonly Key DefaultKey = Key.U;

    public static bool TryParse(string? text, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var mods = ModifierKeys.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!TryParseModifier(parts[i], out var mod) || (mods & mod) != 0)
            {
                return false;
            }

            mods |= mod;
        }

        if (mods == ModifierKeys.None)
        {
            return false;
        }

        if (!TryParseKey(parts[^1], out var parsedKey) || !IsAllowedKey(parsedKey))
        {
            return false;
        }

        modifiers = mods;
        key = parsedKey;
        return true;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None || !IsAllowedKey(key))
        {
            return DefaultDisplay;
        }

        var sb = new StringBuilder(32);
        AppendModifier(sb, modifiers, ModifierKeys.Control, "Ctrl");
        AppendModifier(sb, modifiers, ModifierKeys.Alt, "Alt");
        AppendModifier(sb, modifiers, ModifierKeys.Shift, "Shift");
        AppendModifier(sb, modifiers, ModifierKeys.Windows, "Win");
        if (sb.Length == 0)
        {
            return DefaultDisplay;
        }

        sb.Append('+');
        sb.Append(FormatKey(key));
        return sb.ToString();
    }

    public static string Normalize(string? text)
    {
        if (TryParse(text, out var modifiers, out var key))
        {
            return Format(modifiers, key);
        }

        return DefaultDisplay;
    }

    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;

    public static bool IsAllowedKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return true;
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return true;
        }

        return key is Key.Space or Key.Tab or Key.OemPlus or Key.OemMinus
            or Key.OemComma or Key.OemPeriod or Key.OemQuestion
            or Key.OemOpenBrackets or Key.OemCloseBrackets
            or Key.OemQuotes or Key.OemSemicolon or Key.OemPipe
            or Key.OemTilde or Key.OemBackslash
            or Key.Insert or Key.Delete or Key.Home or Key.End
            or Key.PageUp or Key.PageDown
            or Key.Up or Key.Down or Key.Left or Key.Right;
    }

    private static bool TryParseModifier(string token, out ModifierKeys modifier)
    {
        modifier = ModifierKeys.None;
        switch (token.Trim().ToUpperInvariant())
        {
            case "CTRL":
            case "CONTROL":
                modifier = ModifierKeys.Control;
                return true;
            case "ALT":
                modifier = ModifierKeys.Alt;
                return true;
            case "SHIFT":
                modifier = ModifierKeys.Shift;
                return true;
            case "WIN":
            case "WINDOWS":
            case "META":
                modifier = ModifierKeys.Windows;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseKey(string token, out Key key)
    {
        key = Key.None;
        var t = token.Trim();
        if (t.Length == 1)
        {
            var ch = char.ToUpperInvariant(t[0]);
            if (ch is >= 'A' and <= 'Z')
            {
                key = Key.A + (ch - 'A');
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                key = Key.D0 + (ch - '0');
                return true;
            }
        }

        if (t.StartsWith('F')
            && int.TryParse(t.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fn)
            && fn is >= 1 and <= 24)
        {
            key = Key.F1 + (fn - 1);
            return true;
        }

        switch (t.ToUpperInvariant())
        {
            case "SPACE":
                key = Key.Space;
                return true;
            case "TAB":
                key = Key.Tab;
                return true;
            case "PLUS":
            case "=":
                key = Key.OemPlus;
                return true;
            case "MINUS":
            case "-":
                key = Key.OemMinus;
                return true;
            case "COMMA":
                key = Key.OemComma;
                return true;
            case "PERIOD":
            case "DOT":
                key = Key.OemPeriod;
                return true;
            case "INSERT":
            case "INS":
                key = Key.Insert;
                return true;
            case "DELETE":
            case "DEL":
                key = Key.Delete;
                return true;
            case "HOME":
                key = Key.Home;
                return true;
            case "END":
                key = Key.End;
                return true;
            case "PAGEUP":
            case "PGUP":
                key = Key.PageUp;
                return true;
            case "PAGEDOWN":
            case "PGDN":
                key = Key.PageDown;
                return true;
            case "UP":
                key = Key.Up;
                return true;
            case "DOWN":
                key = Key.Down;
                return true;
            case "LEFT":
                key = Key.Left;
                return true;
            case "RIGHT":
                key = Key.Right;
                return true;
            default:
                return Enum.TryParse(t, ignoreCase: true, out key) && key != Key.None && IsAllowedKey(key);
        }
    }

    private static string FormatKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return ((char)('A' + (key - Key.A))).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return "Num" + (key - Key.NumPad0);
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return "F" + (1 + (key - Key.F1));
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => key.ToString(),
        };
    }

    private static void AppendModifier(StringBuilder sb, ModifierKeys present, ModifierKeys flag, string label)
    {
        if ((present & flag) == 0)
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append('+');
        }

        sb.Append(label);
    }
}
