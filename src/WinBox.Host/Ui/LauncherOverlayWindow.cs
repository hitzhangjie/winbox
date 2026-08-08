using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Minimal top-of-screen launcher input. Shift+Alt+U activates; Esc dismisses.
/// </summary>
internal sealed class LauncherOverlayWindow : Window
{
    private readonly LauncherOverlayState _state;
    private readonly TextBox _queryBox;

    public LauncherOverlayWindow(LauncherOverlayState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));

        Title = "WinBox";
        Width = 560;
        Height = 56;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
        BorderThickness = new Thickness(1);

        _queryBox = new TextBox
        {
            FontSize = 18,
            Margin = new Thickness(12, 10, 12, 10),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CaretBrush = Brushes.White,
        };
        _queryBox.TextChanged += (_, _) => _state.SetQuery(_queryBox.Text);
        Content = _queryBox;

        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void ActivateOverlay()
    {
        _state.Activate();
        _queryBox.Text = string.Empty;

        if (!IsVisible)
        {
            PositionNearTopCenter();
            Show();
        }

        Activate();
        _queryBox.Focus();
    }

    public void DismissOverlay()
    {
        _state.Dismiss();
        _queryBox.Text = string.Empty;
        Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissOverlay();
            e.Handled = true;
        }
    }

    private void PositionNearTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 120;
    }
}
