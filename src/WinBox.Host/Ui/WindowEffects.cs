using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace WinBox.Host.Ui;

/// <summary>
/// Win11 DWM backdrop / rounded corners + overlay fade helpers.
/// Failures are silent: solid theme chrome remains the fallback.
/// </summary>
internal static class WindowEffects
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwcpRound = 2;
    private const int DwmsbtMainWindow = 2; // Mica

    public static void TryEnableSystemChrome(Window window, bool dark)
    {
        ArgumentNullException.ThrowIfNull(window);
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            hwnd = new WindowInteropHelper(window).EnsureHandle();
        }

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var corner = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var darkMode = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        var backdrop = DwmsbtMainWindow;
        _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    public static DropShadowEffect CreateOverlayShadow()
    {
        // Light chrome needs a softer shadow so the white card doesn't look muddy.
        var effect = new DropShadowEffect
        {
            BlurRadius = WinBoxTheme.IsDarkEffective ? 28 : 32,
            ShadowDepth = 0,
            Opacity = WinBoxTheme.IsDarkEffective ? 0.55 : 0.28,
            Color = Colors.Black,
        };
        effect.Freeze();
        return effect;
    }

    /// <summary>Soft elevation for settings cards / panels (lighter than overlay).</summary>
    public static DropShadowEffect CreateCardShadow()
    {
        var effect = new DropShadowEffect
        {
            BlurRadius = WinBoxTheme.IsDarkEffective ? 18 : 20,
            ShadowDepth = 1,
            Direction = 270,
            Opacity = WinBoxTheme.IsDarkEffective ? 0.38 : 0.14,
            Color = Colors.Black,
        };
        effect.Freeze();
        return effect;
    }

    public static void FadeIn(UIElement target, Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(WinBoxTheme.MotionMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        if (completed is not null)
        {
            anim.Completed += (_, _) => completed();
        }

        target.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    public static void FadeOut(UIElement target, Action completed)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(completed);
        var anim = new DoubleAnimation(target.Opacity, 0, TimeSpan.FromMilliseconds(WinBoxTheme.MotionMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        anim.Completed += (_, _) => completed();
        target.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);
}
