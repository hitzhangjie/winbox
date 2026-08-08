using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinBox.Abstractions;

namespace WinBox.Host.Ui.DialogAssist;

/// <summary>
/// Docked search strip under an Open/Save dialog. File search only — no QueryRouter.
/// Colors follow the <em>system</em> light/dark theme so the strip matches Explorer chrome.
/// </summary>
internal sealed class FileDialogAssistBar : Window
{
    private const int DebounceMs = 80;
    private const int ResultLimit = 12;
    private const double StripHeight = 36;
    private const double ResultsMaxHeight = 280;

    private readonly ISearchService _search;
    private readonly IFileDialogPathFiller _filler;
    private readonly FileDialogAssistSession _session = new();
    private readonly TextBox _queryBox;
    private readonly TextBlock _placeholder;
    private readonly TextBlock _searchGlyph;
    private readonly ListBox _results;
    private readonly Border _chrome;
    private readonly Border _resultsChrome;
    private FileDialogTarget _target;
    private CancellationTokenSource? _cts;
    private int _version;
    private bool _syncing;
    private SolidColorBrush _surface = Brushes.White;
    private SolidColorBrush _border = Brushes.Gray;
    private SolidColorBrush _textPrimary = Brushes.Black;
    private SolidColorBrush _textSecondary = Brushes.Gray;
    private SolidColorBrush _selection = Brushes.LightBlue;
    private SolidColorBrush _hover = Brushes.LightGray;
    private HwndSource? _hwndSource;

    public FileDialogAssistBar(ISearchService search, IFileDialogPathFiller filler)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _filler = filler ?? throw new ArgumentNullException(nameof(filler));

        Title = "WinBox";
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        Focusable = true;
        FontFamily = WinBoxTheme.UiFont;
        Width = FileDialogAssistLayout.FixedWidthDip;

        _placeholder = new TextBlock
        {
            Text = "Search files…",
            FontSize = 13,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(34, 0, 12, 0),
        };

        _queryBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(28, 0, 8, 0),
            FocusVisualStyle = null,
        };
        _queryBox.TextChanged += OnQueryChanged;
        _queryBox.PreviewKeyDown += OnQueryPreviewKeyDown;

        _searchGlyph = new TextBlock
        {
            Text = "\uE721",
            FontFamily = WinBoxTheme.GlyphFont,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            IsHitTestVisible = false,
        };

        var inputGrid = new Grid();
        inputGrid.Children.Add(_queryBox);
        inputGrid.Children.Add(_placeholder);
        inputGrid.Children.Add(_searchGlyph);

        _chrome = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(WinBoxTheme.ControlRadius),
            Height = StripHeight,
            Child = inputGrid,
        };

        _results = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FocusVisualStyle = null,
            ItemTemplate = ResultRowView.CreateListTemplate(),
            Visibility = Visibility.Collapsed,
            MaxHeight = ResultsMaxHeight,
        };
        LauncherResultsScroll.Configure(_results);
        _results.MouseDoubleClick += (_, _) => ActivateSelected();
        _results.PreviewKeyDown += OnResultsPreviewKeyDown;
        _results.SelectionChanged += (_, _) =>
        {
            if (_syncing || _results.SelectedIndex < 0)
            {
                return;
            }

            _session.SelectIndex(_results.SelectedIndex);
        };
        _results.ItemContainerGenerator.StatusChanged += (_, _) => PaintResultRows();

        _resultsChrome = new Border
        {
            BorderThickness = new Thickness(1, 0, 1, 1),
            CornerRadius = new CornerRadius(0, 0, WinBoxTheme.ControlRadius, WinBoxTheme.ControlRadius),
            Child = _results,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, -1, 0, 0),
        };

        var root = new DockPanel();
        DockPanel.SetDock(_chrome, Dock.Top);
        root.Children.Add(_chrome);
        root.Children.Add(_resultsChrome);
        Content = root;

        ApplySystemPalette();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) =>
        {
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        };
    }

    public nint EnsureHwnd() => new WindowInteropHelper(this).EnsureHandle();

    public void Attach(FileDialogTarget target)
    {
        ApplySystemPalette();
        _target = target;
        if (!IsVisible)
        {
            Show();
        }

        Reposition(target);
        FocusQuery();
    }

    public void Reposition(FileDialogTarget target)
    {
        _target = target;
        if (!target.IsValid)
        {
            return;
        }

        var hwnd = EnsureHwnd();
        var scale = GetScale(hwnd);
        var (left, top, width) = FileDialogAssistLayout.PlaceUnderDialog(
            target.Left,
            target.Bottom,
            target.Width,
            scale);

        UpdateHeight();

        var heightDip = Height > 0 ? Height : StripHeight;
        var leftPx = (int)Math.Round(left * scale);
        var topPx = (int)Math.Round(top * scale);
        var widthPx = (int)Math.Round(width * scale);
        var heightPx = (int)Math.Round(heightDip * scale);
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopMost,
            leftPx,
            topPx,
            widthPx,
            heightPx,
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);

        Left = left;
        Top = top;
        Width = width;
    }

    /// <summary>Hide during dialog drag/resize so a lagging strip is never visible.</summary>
    public void HideWhileDialogMoves()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    /// <summary>Snap to final dialog bounds and show again after drag/resize ends.</summary>
    public void ShowAfterDialogMoved(FileDialogTarget target)
    {
        _target = target;
        if (!target.IsValid)
        {
            return;
        }

        Reposition(target);
        if (!IsVisible)
        {
            Show();
        }
    }

    public void Detach()
    {
        _cts?.Cancel();
        _session.SetQuery(string.Empty);
        _session.ClearResults();
        _syncing = true;
        _queryBox.Text = string.Empty;
        _syncing = false;
        _placeholder.Visibility = Visibility.Visible;
        _results.ItemsSource = null;
        _results.Visibility = Visibility.Collapsed;
        _resultsChrome.Visibility = Visibility.Collapsed;
        Hide();
    }

    public void FocusQuery()
    {
        Activate();
        _queryBox.Focus();
        _queryBox.SelectAll();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int wmSettingChange = 0x001A;
        if (msg == wmSettingChange)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, ApplySystemPalette);
        }

        return 0;
    }

    private void ApplySystemPalette()
    {
        var colors = WinBoxTheme.Resolve(WinBoxTheme.DetectSystemTheme());
        _surface = Freeze(colors.SurfaceOverlay);
        _border = Freeze(colors.BorderSubtle);
        _textPrimary = Freeze(colors.TextPrimary);
        _textSecondary = Freeze(colors.TextSecondary);
        _selection = Freeze(colors.Selection);
        _hover = Freeze(colors.Hover);

        _chrome.Background = _surface;
        _chrome.BorderBrush = _border;
        _resultsChrome.Background = _surface;
        _resultsChrome.BorderBrush = _border;
        _queryBox.Foreground = _textPrimary;
        _queryBox.CaretBrush = _textPrimary;
        _placeholder.Foreground = _textSecondary;
        _searchGlyph.Foreground = _textSecondary;
        _results.ItemContainerStyle = CreateResultItemStyle();
        PaintResultRows();
    }

    private void PaintResultRows()
    {
        if (_results.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        for (var i = 0; i < _results.Items.Count; i++)
        {
            if (_results.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
            {
                continue;
            }

            if (FindResultRow(container) is { } row)
            {
                row.ApplyThemeBrushes(_textPrimary, _textSecondary);
            }
        }
    }

    private static ResultRowView? FindResultRow(DependencyObject root)
    {
        if (root is ResultRowView row)
        {
            return row;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindResultRow(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        var text = _queryBox.Text ?? string.Empty;
        _placeholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _session.SetQuery(text);
        ScheduleSearch();
    }

    private void ScheduleSearch()
    {
        var version = Interlocked.Increment(ref _version);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = RunSearchAsync(version, token);
    }

    private async Task RunSearchAsync(int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMs, token).ConfigureAwait(true);
            if (version != Volatile.Read(ref _version))
            {
                return;
            }

            var text = _session.Query;
            if (string.IsNullOrWhiteSpace(text))
            {
                _session.ClearResults();
                ApplyResults();
                return;
            }

            var hits = await _search.SearchAsync(
                new SearchQuery(Text: text, Limit: ResultLimit),
                token).ConfigureAwait(true);
            if (version != Volatile.Read(ref _version) || token.IsCancellationRequested)
            {
                return;
            }

            _session.SetResults(hits);
            ApplyResults();
        }
        catch (OperationCanceledException)
        {
            // newer keystroke
        }
    }

    private void ApplyResults()
    {
        var hits = _session.Results;
        if (hits.Count == 0)
        {
            _results.ItemsSource = null;
            _results.Visibility = Visibility.Collapsed;
            _resultsChrome.Visibility = Visibility.Collapsed;
            _chrome.CornerRadius = new CornerRadius(WinBoxTheme.ControlRadius);
            UpdateHeight();
            Reposition(_target);
            return;
        }

        var rows = hits.Select(h => new ResultRowModel
        {
            Title = h.Name,
            Subtitle = h.Path,
            ToolTipText = h.Path,
            Action = ResultActionKind.OpenPath,
            IconImage = ShellFileIcons.GetForPath(h.Path),
        }).ToList();

        _syncing = true;
        _results.ItemsSource = rows;
        _results.SelectedIndex = _session.SelectedIndex;
        _syncing = false;
        _results.Visibility = Visibility.Visible;
        _resultsChrome.Visibility = Visibility.Visible;
        _chrome.CornerRadius = new CornerRadius(WinBoxTheme.ControlRadius, WinBoxTheme.ControlRadius, 0, 0);
        UpdateHeight();
        Reposition(_target);
        ScrollSelectedIntoView();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, PaintResultRows);
    }

    private void UpdateHeight()
    {
        var resultsHeight = _resultsChrome.Visibility == Visibility.Visible
            ? (_resultsChrome.ActualHeight > 1 ? _resultsChrome.ActualHeight : EstimateResultsHeight())
            : 0;
        Height = StripHeight + resultsHeight;
    }

    private double EstimateResultsHeight()
    {
        var count = Math.Min(_session.Results.Count, 6);
        return count * (WinBoxTheme.ResultRowMinHeight + 4) + 8;
    }

    private void OnQueryPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_session.MoveSelection(1))
                {
                    SyncListSelection();
                    e.Handled = true;
                }

                break;
            case Key.Up:
                if (_session.MoveSelection(-1))
                {
                    SyncListSelection();
                    e.Handled = true;
                }

                break;
            case Key.Enter:
                ActivateSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                if (!string.IsNullOrEmpty(_queryBox.Text) || _session.Results.Count > 0)
                {
                    _syncing = true;
                    _queryBox.Text = string.Empty;
                    _syncing = false;
                    _session.SetQuery(string.Empty);
                    _session.ClearResults();
                    ApplyResults();
                    _placeholder.Visibility = Visibility.Visible;
                }

                e.Handled = true;
                break;
        }
    }

    private void OnResultsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FocusQuery();
            e.Handled = true;
        }
    }

    private void SyncListSelection()
    {
        _syncing = true;
        _results.SelectedIndex = _session.SelectedIndex;
        _syncing = false;
        ScrollSelectedIntoView();
    }

    private void ScrollSelectedIntoView()
    {
        if (_results.SelectedItem is not null)
        {
            _results.ScrollIntoView(_results.SelectedItem);
        }
    }

    private void ActivateSelected()
    {
        var hit = _session.SelectedHit;
        if (hit is null || !_target.IsValid)
        {
            return;
        }

        if (_filler.TryFill(_target, hit.Path))
        {
            NativeMethods.SetForegroundWindow(_target.DialogHwnd);
            _syncing = true;
            _queryBox.Text = hit.Name;
            _syncing = false;
            _placeholder.Visibility = Visibility.Collapsed;
            _session.ClearResults();
            ApplyResults();
        }
    }

    private Style CreateResultItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(2, 1, 2, 1)));
        style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, CreateResultItemTemplate()));

        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, _selection));
        style.Triggers.Add(selected);

        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(ListBoxItem.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
        hover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, _hover));
        style.Triggers.Add(hover);

        return style;
    }

    private static ControlTemplate CreateResultItemTemplate()
    {
        var template = new ControlTemplate(typeof(ListBoxItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(WinBoxTheme.ControlRadius));
        border.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(Background))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static double GetScale(nint hwnd)
    {
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    private static class NativeMethods
    {
        public static readonly nint HwndTopMost = new(-1);

        public const uint SwpNoActivate = 0x0010;
        public const uint SwpNoOwnerZOrder = 0x0200;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
    }
}
