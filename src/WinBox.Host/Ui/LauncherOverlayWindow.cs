using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>
/// Launcher shell: mode chrome + input + two-line results + footer.
/// Routing is owned by <see cref="LauncherQuerySession"/>.
/// </summary>
internal sealed class LauncherOverlayWindow : Window
{
    private readonly LauncherOverlayState _state;
    private readonly LauncherQuerySession _session;
    private readonly UiOptionsStore _uiStore;
    private readonly TextBlock _modeLabel;
    private readonly TextBlock _modeSeparator;
    private readonly TextBox _queryBox;
    private readonly TextBlock _placeholder;
    private readonly ListBox _results;
    private readonly StackPanel _emptyPanel;
    private readonly TextBlock _emptyTitle;
    private readonly TextBlock _emptyDetail;
    private readonly TextBlock _footerText;
    private readonly Border _chrome;
    private readonly Border _divider;
    private readonly DispatcherTimer _persistTimer;
    private UiOptions _uiOptions;
    private bool _syncingUi;
    private bool _suppressPersist;
    private bool _dismissing;

    public LauncherOverlayWindow(
        LauncherOverlayState state,
        LauncherQuerySession session,
        UiOptionsStore uiStore)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _uiStore = uiStore ?? throw new ArgumentNullException(nameof(uiStore));
        _uiOptions = UiOptionsStore.Normalize(_uiStore.LoadOrDefault());
        UiLayout.Apply(_uiOptions);

        Title = "WinBox";
        Width = UiLayout.OverlayWidth + (WinBoxTheme.OverlayShadowPad * 2);
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        FontFamily = WinBoxTheme.UiFont;

        _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _persistTimer.Tick += (_, _) =>
        {
            _persistTimer.Stop();
            PersistPosition();
        };

        _chrome = new Border
        {
            CornerRadius = new CornerRadius(WinBoxTheme.OverlayRadius),
            Background = WinBoxTheme.SurfaceOverlayBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            SnapsToDevicePixels = true,
            Effect = WindowEffects.CreateOverlayShadow(),
            Margin = new Thickness(WinBoxTheme.OverlayShadowPad),
        };
        _chrome.MouseLeftButtonDown += OnChromeMouseLeftButtonDown;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(WinBoxTheme.InputRowHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var inputRow = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 0, 16, 0) };

        _modeLabel = new TextBlock
        {
            FontSize = UiLayout.FontInput,
            FontWeight = FontWeights.SemiBold,
            Foreground = WinBoxTheme.AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 6, 0),
        };
        _modeSeparator = new TextBlock
        {
            Text = "|",
            FontSize = UiLayout.FontInput,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 8, 0),
        };
        DockPanel.SetDock(_modeLabel, Dock.Left);
        DockPanel.SetDock(_modeSeparator, Dock.Left);
        inputRow.Children.Add(_modeLabel);
        inputRow.Children.Add(_modeSeparator);

        var queryHost = new Grid();
        _placeholder = new TextBlock
        {
            Text = LauncherChromeText.Placeholder,
            FontSize = UiLayout.FontInput,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0.72,
        };
        _queryBox = new TextBox
        {
            FontSize = UiLayout.FontInput,
            FontFamily = WinBoxTheme.UiFont,
            Background = Brushes.Transparent,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            CaretBrush = WinBoxTheme.AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _queryBox.TextChanged += OnQueryTextChanged;
        queryHost.Children.Add(_placeholder);
        queryHost.Children.Add(_queryBox);
        inputRow.Children.Add(queryHost);
        Grid.SetRow(inputRow, 0);
        root.Children.Add(inputRow);

        var resultsHost = new Grid();
        _results = new ListBox
        {
            Visibility = Visibility.Collapsed,
            MaxHeight = UiLayout.ResultsMaxHeight,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            FontFamily = WinBoxTheme.UiFont,
            Padding = new Thickness(6, 6, 6, 6),
            ItemTemplate = ResultRowView.CreateListTemplate(),
            ItemContainerStyle = CreateResultItemStyle(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        // Attached scroll policy: vertical only — never grow width for long paths.
        ScrollViewer.SetHorizontalScrollBarVisibility(_results, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_results, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
        ScrollViewer.SetCanContentScroll(_results, true);
        ThemedScrollBars.Apply(_results);

        _results.SelectionChanged += (_, _) =>
        {
            if (_syncingUi || _results.SelectedIndex < 0)
            {
                return;
            }

            _state.SetSelectedIndex(_results.SelectedIndex);
            RefreshFooter();
        };
        _results.MouseDoubleClick += async (_, _) =>
        {
            await _session.ActivateSelectedAsync().ConfigureAwait(true);
            DismissOverlay();
        };

        _emptyTitle = new TextBlock
        {
            FontSize = UiLayout.FontTitle,
            FontWeight = FontWeights.SemiBold,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            TextWrapping = TextWrapping.Wrap,
        };
        _emptyDetail = new TextBlock
        {
            FontSize = UiLayout.FontSubtitle,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        _emptyPanel = new StackPanel
        {
            Margin = new Thickness(18, 16, 18, 16),
            Visibility = Visibility.Collapsed,
        };
        _emptyPanel.Children.Add(_emptyTitle);
        _emptyPanel.Children.Add(_emptyDetail);

        resultsHost.Children.Add(_results);
        resultsHost.Children.Add(_emptyPanel);
        Grid.SetRow(resultsHost, 1);
        root.Children.Add(resultsHost);

        _footerText = new TextBlock
        {
            Text = LauncherChromeText.FooterFor(null, hasResults: false),
            FontSize = UiLayout.FontFooter,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            Margin = new Thickness(16, 8, 16, 12),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.9,
        };
        Grid.SetRow(_footerText, 2);
        root.Children.Add(_footerText);

        // Top hairline under the input when results/empty are shown.
        _divider = new Border
        {
            Height = 1,
            Background = WinBoxTheme.BorderSubtleBrush,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Opacity = 0.85,
        };
        Grid.SetRow(_divider, 1);
        root.Children.Add(_divider);
        _results.IsVisibleChanged += (_, _) => RefreshDivider();
        _emptyPanel.IsVisibleChanged += (_, _) => RefreshDivider();

        _chrome.Child = root;
        Content = _chrome;

        PreviewKeyDown += OnPreviewKeyDown;
        LocationChanged += (_, _) => SchedulePersist();
        _state.Changed += () => Dispatcher.Invoke(SyncFromState);
        WinBoxTheme.Changed += OnThemeChanged;
        UiLayout.Changed += OnLayoutChanged;
        Closed += (_, _) =>
        {
            WinBoxTheme.Changed -= OnThemeChanged;
            UiLayout.Changed -= OnLayoutChanged;
        };
    }

    private void OnLayoutChanged()
    {
        Dispatcher.Invoke(ApplyChromeFromLayout);
    }

    private void ApplyChromeFromLayout()
    {
        _uiOptions = UiOptionsStore.Normalize(_uiStore.LoadOrDefault());
        Width = UiLayout.OverlayWidth + (WinBoxTheme.OverlayShadowPad * 2);
        _modeLabel.FontSize = UiLayout.FontInput;
        _modeSeparator.FontSize = UiLayout.FontInput;
        _queryBox.FontSize = UiLayout.FontInput;
        _placeholder.FontSize = UiLayout.FontInput;
        _results.MaxHeight = UiLayout.ResultsMaxHeight;
        _emptyTitle.FontSize = UiLayout.FontTitle;
        _emptyDetail.FontSize = UiLayout.FontSubtitle;
        _footerText.FontSize = UiLayout.FontFooter;
        ScrollViewer.SetVerticalScrollBarVisibility(_results, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
        ThemedScrollBars.Apply(_results);
        _results.ItemTemplate = ResultRowView.CreateListTemplate();
        SyncFromState();
    }

    private void OnThemeChanged()
    {
        Dispatcher.Invoke(() =>
        {
            _chrome.Background = WinBoxTheme.SurfaceOverlayBrush;
            _chrome.BorderBrush = WinBoxTheme.BorderSubtleBrush;
            _chrome.Effect = WindowEffects.CreateOverlayShadow();
            _modeLabel.Foreground = WinBoxTheme.AccentBrush;
            _modeSeparator.Foreground = WinBoxTheme.TextSecondaryBrush;
            _queryBox.Foreground = WinBoxTheme.TextPrimaryBrush;
            _queryBox.CaretBrush = WinBoxTheme.AccentBrush;
            _placeholder.Foreground = WinBoxTheme.TextSecondaryBrush;
            _results.Background = Brushes.Transparent;
            _results.Foreground = WinBoxTheme.TextPrimaryBrush;
            _results.ItemContainerStyle = CreateResultItemStyle();
            _results.ItemTemplate = ResultRowView.CreateListTemplate();
            ThemedScrollBars.Apply(_results);
            _emptyTitle.Foreground = WinBoxTheme.TextPrimaryBrush;
            _emptyDetail.Foreground = WinBoxTheme.TextSecondaryBrush;
            _footerText.Foreground = WinBoxTheme.TextSecondaryBrush;
            _divider.Background = WinBoxTheme.BorderSubtleBrush;
            SyncFromState();
        });
    }

    public void ActivateOverlay()
    {
        _dismissing = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        _state.Activate();
        _syncingUi = true;
        _queryBox.Text = string.Empty;
        _syncingUi = false;

        if (!IsVisible)
        {
            _suppressPersist = true;
            ApplyPlacement();
            Opacity = 0;
            Show();
            _suppressPersist = false;
            WindowEffects.FadeIn(this);
        }

        Activate();
        _queryBox.Focus();
        SyncFromState();
    }

    public void DismissOverlay()
    {
        if (_dismissing)
        {
            return;
        }

        PersistPosition();
        _state.Dismiss();
        _syncingUi = true;
        _queryBox.Text = string.Empty;
        _syncingUi = false;

        if (!IsVisible)
        {
            return;
        }

        _dismissing = true;
        WindowEffects.FadeOut(this, () =>
        {
            Hide();
            Opacity = 1;
            _dismissing = false;
        });
    }

    private void OnChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // Allow dragging from chrome / labels; keep TextBox and list interaction intact.
        if (e.OriginalSource is TextBoxBase or ListBoxItem || IsDescendantOf(_queryBox, e.OriginalSource as DependencyObject)
            || IsDescendantOf(_results, e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse is not pressed anymore.
        }

        PersistPosition();
    }

    private static bool IsDescendantOf(DependencyObject? root, DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, root))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void OnQueryTextChanged(object sender, TextChangedEventArgs e)
    {
        _placeholder.Visibility = string.IsNullOrEmpty(_queryBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

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
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            DismissOverlay();
            e.Handled = true;
            return;
        }

        if (key == Key.Back
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

        if (key == Key.Down)
        {
            _state.SelectNext();
            e.Handled = true;
            return;
        }

        if (key == Key.Up)
        {
            _state.SelectPrevious();
            e.Handled = true;
            return;
        }

        if (key == Key.Enter)
        {
            await _session.ActivateSelectedAsync(ResolveEnterActionOverride()).ConfigureAwait(true);
            DismissOverlay();
            e.Handled = true;
        }
    }

    private ResultActionKind? ResolveEnterActionOverride()
    {
        var item = _state.SelectedItem;
        if (item is null)
        {
            return null;
        }

        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        return PathActivationShortcuts.ResolveOpenPathOverride(item.Action, alt);
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

            var hasQuery = !string.IsNullOrWhiteSpace(_state.Query);
            var hasResults = _state.Results.Count > 0;
            _placeholder.Visibility = string.IsNullOrEmpty(_queryBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            _results.Items.Clear();
            if (hasResults)
            {
                foreach (var item in _state.Results)
                {
                    _results.Items.Add(ResultRowModel.FromResult(item));
                }

                _results.Visibility = Visibility.Visible;
                _emptyPanel.Visibility = Visibility.Collapsed;

                if (_state.SelectedIndex >= 0 && _state.SelectedIndex < _results.Items.Count)
                {
                    _results.SelectedIndex = _state.SelectedIndex;
                    _results.ScrollIntoView(_results.SelectedItem);
                }
            }
            else if (hasQuery)
            {
                _results.Visibility = Visibility.Collapsed;
                _emptyTitle.Text = LauncherChromeText.NoResultsTitle(_state.Query);
                _emptyDetail.Text = LauncherChromeText.NoResultsDetail;
                _emptyPanel.Visibility = Visibility.Visible;
            }
            else
            {
                _results.Visibility = Visibility.Collapsed;
                _emptyTitle.Text = LauncherChromeText.IdleHint;
                _emptyDetail.Text = LauncherChromeText.IdleDetail;
                _emptyPanel.Visibility = Visibility.Visible;
            }

            RefreshFooter();
            RefreshDivider();
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private void RefreshFooter()
    {
        var hasResults = _state.Results.Count > 0;
        var action = _state.SelectedItem?.Action;
        _footerText.Text = LauncherChromeText.FooterFor(action, hasResults);
    }

    private void RefreshDivider()
    {
        _divider.Visibility = _results.Visibility == Visibility.Visible || _emptyPanel.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyPlacement()
    {
        Width = UiLayout.OverlayWidth + (WinBoxTheme.OverlayShadowPad * 2);

        if (_uiOptions.OverlayLeft is double left && _uiOptions.OverlayTop is double top
            && IsPlacementVisible(left, top, UiLayout.OverlayWidth))
        {
            Left = left;
            Top = top;
            return;
        }

        PositionNearTopCenter();
    }

    private static bool IsPlacementVisible(double left, double top, double width)
    {
        var work = SystemParameters.WorkArea;
        // Require the overlay header to sit mostly inside the work area.
        var probeX = left + Math.Min(80, width / 2);
        var probeY = top + 24;
        return probeX >= work.Left && probeX <= work.Right
            && probeY >= work.Top && probeY <= work.Bottom;
    }

    private void PositionNearTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 120;
    }

    private void SchedulePersist()
    {
        if (_suppressPersist || !IsVisible)
        {
            return;
        }

        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void PersistPosition()
    {
        if (_suppressPersist)
        {
            return;
        }

        var current = UiOptionsStore.Normalize(_uiStore.LoadOrDefault());
        _uiOptions = new UiOptions
        {
            OverlayLeft = Left,
            OverlayTop = Top,
            OverlayWidth = Math.Max(400, Width - (WinBoxTheme.OverlayShadowPad * 2)),
            ResultsMaxHeight = current.ResultsMaxHeight,
            FontInput = current.FontInput,
            FontTitle = current.FontTitle,
            FontSubtitle = current.FontSubtitle,
            ScrollBarWidth = current.ScrollBarWidth,
            ScrollBarMode = current.ScrollBarMode,
            Theme = current.Theme,
        };
        try
        {
            _uiStore.Save(_uiOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-fatal: launcher still works without persistence.
        }
    }

    private static Style CreateResultItemStyle()
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
        selected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, WinBoxTheme.SelectionBrush));
        style.Triggers.Add(selected);

        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(ListBoxItem.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
        hover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, WinBoxTheme.HoverBrush));
        style.Triggers.Add(hover);

        return style;
    }

    private static ControlTemplate CreateResultItemTemplate()
    {
        var template = new ControlTemplate(typeof(ListBoxItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
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
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }
}
