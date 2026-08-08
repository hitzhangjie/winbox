using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>
/// Launcher shell: mode chrome (e.g. Google |) + input + results dropdown.
/// Routing is owned by <see cref="LauncherQuerySession"/>.
/// </summary>
internal sealed class LauncherOverlayWindow : Window
{
    private const double InputWidth = 560;
    private const double InputRowHeight = 56;
    private const double ResultsMaxHeight = 280;

    private readonly LauncherOverlayState _state;
    private readonly LauncherQuerySession _session;
    private readonly TextBlock _modeLabel;
    private readonly TextBlock _modeSeparator;
    private readonly TextBox _queryBox;
    private readonly ListBox _results;
    private readonly Grid _root;
    private bool _syncingUi;

    public LauncherOverlayWindow(LauncherOverlayState state, LauncherQuerySession session)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        Title = "WinBox";
        Width = InputWidth;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
        BorderThickness = new Thickness(1);

        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(InputRowHeight) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var inputRow = new DockPanel { LastChildFill = true, Margin = new Thickness(12, 0, 12, 0) };

        _modeLabel = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0xC7, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 6, 0),
        };
        _modeSeparator = new TextBlock
        {
            Text = "|",
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 8, 0),
        };
        DockPanel.SetDock(_modeLabel, Dock.Left);
        DockPanel.SetDock(_modeSeparator, Dock.Left);
        inputRow.Children.Add(_modeLabel);
        inputRow.Children.Add(_modeSeparator);

        _queryBox = new TextBox
        {
            FontSize = 18,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CaretBrush = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _queryBox.TextChanged += OnQueryTextChanged;
        inputRow.Children.Add(_queryBox);
        Grid.SetRow(inputRow, 0);
        _root.Children.Add(inputRow);

        _results = new ListBox
        {
            Visibility = Visibility.Collapsed,
            MaxHeight = ResultsMaxHeight,
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = Brushes.White,
            FontSize = 14,
        };
        _results.SelectionChanged += (_, _) =>
        {
            if (_syncingUi || _results.SelectedIndex < 0)
            {
                return;
            }

            _state.SetSelectedIndex(_results.SelectedIndex);
        };
        _results.MouseDoubleClick += async (_, _) =>
        {
            await _session.ActivateSelectedAsync().ConfigureAwait(true);
            DismissOverlay();
        };
        Grid.SetRow(_results, 1);
        _root.Children.Add(_results);

        Content = _root;
        PreviewKeyDown += OnPreviewKeyDown;
        _state.Changed += () => Dispatcher.Invoke(SyncFromState);
    }

    public void ActivateOverlay()
    {
        _state.Activate();
        _syncingUi = true;
        _queryBox.Text = string.Empty;
        _syncingUi = false;

        if (!IsVisible)
        {
            PositionNearTopCenter();
            Show();
        }

        Activate();
        _queryBox.Focus();
        SyncFromState();
    }

    public void DismissOverlay()
    {
        _state.Dismiss();
        _syncingUi = true;
        _queryBox.Text = string.Empty;
        _syncingUi = false;
        Hide();
    }

    private void OnQueryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingUi)
        {
            return;
        }

        string raw;
        if (!string.IsNullOrEmpty(_state.ModeLabel))
        {
            raw = _state.ComposeRawFromPayload(_queryBox.Text);
        }
        else
        {
            raw = _queryBox.Text;
            _state.SetRawQuery(raw);
        }

        _session.NotifyTextChanged(raw);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissOverlay();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back
            && _queryBox.CaretIndex == 0
            && string.IsNullOrEmpty(_queryBox.Text)
            && !string.IsNullOrEmpty(_state.ModeLabel))
        {
            _syncingUi = true;
            _queryBox.Text = string.Empty;
            _syncingUi = false;
            _state.SetRawQuery(string.Empty);
            _session.NotifyTextChanged(string.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            _state.SelectNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            _state.SelectPrevious();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await _session.ActivateSelectedAsync().ConfigureAwait(true);
            DismissOverlay();
            e.Handled = true;
        }
    }

    private void SyncFromState()
    {
        _syncingUi = true;
        try
        {
            if (!string.IsNullOrEmpty(_state.ModeLabel))
            {
                _modeLabel.Text = _state.ModeLabel;
                _modeLabel.Visibility = Visibility.Visible;
                _modeSeparator.Visibility = Visibility.Visible;
                if (_queryBox.Text != _state.Payload)
                {
                    var caret = _queryBox.CaretIndex;
                    _queryBox.Text = _state.Payload;
                    _queryBox.CaretIndex = Math.Min(caret, _queryBox.Text.Length);
                }
            }
            else
            {
                _modeLabel.Visibility = Visibility.Collapsed;
                _modeSeparator.Visibility = Visibility.Collapsed;
                if (_queryBox.Text != _state.Query)
                {
                    var caret = _queryBox.CaretIndex;
                    _queryBox.Text = _state.Query;
                    _queryBox.CaretIndex = Math.Min(caret, _queryBox.Text.Length);
                }
            }

            _results.Items.Clear();
            foreach (var item in _state.Results)
            {
                _results.Items.Add(FormatResult(item));
            }

            _results.Visibility = _state.Results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_state.SelectedIndex >= 0 && _state.SelectedIndex < _results.Items.Count)
            {
                _results.SelectedIndex = _state.SelectedIndex;
                _results.ScrollIntoView(_results.SelectedItem);
            }
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private static string FormatResult(QueryResultItem item)
    {
        return string.IsNullOrEmpty(item.Subtitle)
            ? item.Title
            : $"{item.Title}  —  {item.Subtitle}";
    }

    private void PositionNearTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 120;
    }
}
