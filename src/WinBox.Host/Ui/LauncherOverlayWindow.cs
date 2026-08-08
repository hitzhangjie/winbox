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
    private readonly Button _expandButton;
    private readonly TextBlock _expandGlyph;
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
    private bool _streamFollowPinned = true;
    private bool _ignoreStreamScroll;
    private ScrollViewer? _resultsScroll;

    /// <summary>Raised when the user clicks the File Search expand affordance.</summary>
    public event Action<string>? OpenFileSearchRequested;

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

        _expandButton = CreateExpandButton(out _expandGlyph);
        DockPanel.SetDock(_expandButton, Dock.Right);
        inputRow.Children.Add(_expandButton);

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
            Margin = new Thickness(0, 0, 8, 0),
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
        // Vertical themed scrollbar (settings-style); pixel scroll for tall AI bodies.
        LauncherResultsScroll.Configure(_results);
        _results.Loaded += (_, _) => EnsureResultsScrollHooked();

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
            var action = _state.SelectedItem?.Action;
            if (action is null or ResultActionKind.None)
            {
                return;
            }

            await _session.ActivateSelectedAsync().ConfigureAwait(true);
            if (LauncherQuerySession.ShouldDismissAfterActivate(action))
            {
                DismissOverlay();
            }
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
        LauncherResultsScroll.Configure(_results);
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
            _expandGlyph.Foreground = WinBoxTheme.TextSecondaryBrush;
            _expandButton.Background = Brushes.Transparent;
            _expandButton.BorderBrush = Brushes.Transparent;
            _expandButton.Template = CreateExpandButtonTemplate();
            ToolTipService.SetToolTip(_expandButton, FileSearchChromeText.ExpandTooltip);
            _results.Background = Brushes.Transparent;
            _results.Foreground = WinBoxTheme.TextPrimaryBrush;
            _results.ItemContainerStyle = CreateResultItemStyle();
            _results.ItemTemplate = ResultRowView.CreateListTemplate();
            LauncherResultsScroll.Configure(_results);
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
        _streamFollowPinned = true;
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
        else
        {
            ApplyBaseOverlayWidth();
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
        if (e.OriginalSource is TextBoxBase or ListBoxItem or ButtonBase
            || IsDescendantOf(_queryBox, e.OriginalSource as DependencyObject)
            || IsDescendantOf(_expandButton, e.OriginalSource as DependencyObject)
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
            e.Handled = true;
            var action = ResolveEnterActionOverride() ?? _state.SelectedItem?.Action;
            // Idle / streaming (None): keep overlay open, do nothing.
            if (action is null or ResultActionKind.None)
            {
                return;
            }

            await _session.ActivateSelectedAsync(ResolveEnterActionOverride()).ConfigureAwait(true);
            // Submit (send AI) stays open to show the stream; CopyText / open dismiss.
            if (LauncherQuerySession.ShouldDismissAfterActivate(action))
            {
                DismissOverlay();
            }
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
                    // Tall multiline AI rows use pixel scroll; ScrollIntoView fights the stream follow.
                    if (!_state.Results.Any(static r => r.Multiline))
                    {
                        _results.ScrollIntoView(_results.SelectedItem);
                    }
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
            if (string.Equals(_state.SelectedItem?.Id, "ai-pending", StringComparison.Ordinal))
            {
                _streamFollowPinned = true;
            }

            ApplyOverlayWidthForResults();
            RelayoutOverlayHeight();
            EnsureResultsScrollHooked();
            FollowMultilineStreamScroll();
        }
        finally
        {
            _syncingUi = false;
        }
    }

    /// <summary>
    /// Widen the overlay for multiline AI bodies up to 1.5× the configured base width.
    /// Resets to base when not showing multiline content.
    /// </summary>
    private void ApplyOverlayWidthForResults()
    {
        var baseWidth = UiLayout.OverlayWidth;
        var contentWidth = baseWidth;
        var multiline = _state.Results.FirstOrDefault(static r => r.Multiline);
        if (multiline is not null && !string.IsNullOrWhiteSpace(multiline.Title))
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var estimated = LauncherOverlayFit.EstimatePreferredContentWidth(
                multiline.Title,
                UiLayout.FontTitle,
                WinBoxTheme.UiFont,
                dpi);
            contentWidth = LauncherOverlayFit.ClampContentWidth(baseWidth, estimated);
        }

        var target = contentWidth + (WinBoxTheme.OverlayShadowPad * 2);
        if (Math.Abs(Width - target) > 0.5)
        {
            Width = target;
        }
    }

    private void ApplyBaseOverlayWidth()
    {
        Width = UiLayout.OverlayWidth + (WinBoxTheme.OverlayShadowPad * 2);
    }

    /// <summary>
    /// While AI is streaming and the user has not scrolled away, keep the viewport at the bottom.
    /// </summary>
    private void FollowMultilineStreamScroll()
    {
        if (_dismissing
            || !IsVisible
            || _results.Visibility != Visibility.Visible
            || _state.SelectedItem is not { Multiline: true, Action: ResultActionKind.None } item)
        {
            return;
        }

        if (!LauncherOverlayFit.ShouldFollowStream(_streamFollowPinned, isStreamingMultiline: true))
        {
            return;
        }

        // Pending / streaming ids only — finished CopyText answers stay where the user left them.
        if (item.Id is not ("ai-pending" or "ai-stream"))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                if (_dismissing || !IsVisible || !_streamFollowPinned)
                {
                    return;
                }

                var scroll = FindDescendantScrollViewer(_results);
                if (scroll is null)
                {
                    return;
                }

                _ignoreStreamScroll = true;
                try
                {
                    scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
                }
                finally
                {
                    _ignoreStreamScroll = false;
                }
            },
            DispatcherPriority.Loaded);
    }

    private void EnsureResultsScrollHooked()
    {
        var scroll = FindDescendantScrollViewer(_results);
        if (scroll is null || ReferenceEquals(scroll, _resultsScroll))
        {
            return;
        }

        if (_resultsScroll is not null)
        {
            _resultsScroll.ScrollChanged -= OnResultsScrollChanged;
        }

        _resultsScroll = scroll;
        _resultsScroll.ScrollChanged += OnResultsScrollChanged;
    }

    private void OnResultsScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingUi || _ignoreStreamScroll || _dismissing || !_streamFollowPinned)
        {
            return;
        }

        // Stream appends grow ExtentHeight without a user scroll — don't unpin.
        if (e.ExtentHeightChange != 0 && e.VerticalChange == 0)
        {
            return;
        }

        // Ignore no-op layout noise.
        if (e.VerticalChange == 0 && e.ViewportHeightChange == 0)
        {
            return;
        }

        if (sender is not ScrollViewer scroll)
        {
            return;
        }

        // User scrolled away from the bottom — stop auto-follow for the rest of this stream.
        if (!LauncherOverlayFit.IsNearBottom(scroll.VerticalOffset, scroll.ViewportHeight, scroll.ExtentHeight))
        {
            _streamFollowPinned = false;
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer self)
        {
            return self;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindDescendantScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// <see cref="SizeToContent.Height"/> often sticks after content changes; nudge measure so
    /// streaming / multi-line AI rows expand within <see cref="UiLayout.ResultsMaxHeight"/>.
    /// </summary>
    private void RelayoutOverlayHeight()
    {
        if (!IsVisible || _dismissing)
        {
            return;
        }

        // Keep the results pane hard-capped so the overlay cannot grow without limit.
        _results.MaxHeight = UiLayout.ResultsMaxHeight;

        SizeToContent = SizeToContent.Manual;
        InvalidateMeasure();
        UpdateLayout();
        SizeToContent = SizeToContent.Height;
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

    private Button CreateExpandButton(out TextBlock glyph)
    {
        // "⋯" reads as More / 查看更多 — clearer than search+pop-out badge.
        glyph = new TextBlock
        {
            Text = "\uE712",
            FontFamily = WinBoxTheme.GlyphFont,
            FontSize = 16,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

        var button = new Button
        {
            Content = glyph,
            Width = 32,
            Height = 32,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            FocusVisualStyle = null,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Template = CreateExpandButtonTemplate(),
        };
        ToolTipService.SetToolTip(button, FileSearchChromeText.ExpandTooltip);
        var glyphRef = glyph;
        button.MouseEnter += (_, _) => glyphRef.Foreground = WinBoxTheme.TextPrimaryBrush;
        button.MouseLeave += (_, _) =>
        {
            if (!button.IsMouseOver)
            {
                glyphRef.Foreground = WinBoxTheme.TextSecondaryBrush;
            }
        };
        button.Click += (_, _) =>
        {
            var seed = string.IsNullOrEmpty(_state.ModeLabel) ? _queryBox.Text : _state.Payload;
            OpenFileSearchRequested?.Invoke(seed ?? string.Empty);
            DismissOverlay();
        };
        return button;
    }

    /// <summary>
    /// Soft pill hover using theme HoverBrush — avoids the stock WPF light-blue flash.
    /// </summary>
    private static ControlTemplate CreateExpandButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(WinBoxTheme.ControlRadius));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, WinBoxTheme.HoverBrush) { TargetName = "Bd" });
        template.Triggers.Add(hover);

        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, WinBoxTheme.SelectionBrush) { TargetName = "Bd" });
        template.Triggers.Add(pressed);

        return template;
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
            // Persist the configured base width — not a temporary AI widen.
            OverlayWidth = UiLayout.OverlayWidth,
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
