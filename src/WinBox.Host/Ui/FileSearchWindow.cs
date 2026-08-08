using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinBox.Abstractions;
using WinBox.Search;
using WinBox.Search.Query;

namespace WinBox.Host.Ui;

/// <summary>
/// Host-owned Listary-style File Search: keyword + left filters + results table.
/// Queries <see cref="ISearchService"/> directly (not the toolbox router).
/// </summary>
internal sealed class FileSearchWindow : Window
{
    private const int DebounceMs = 120;
    private const double SidebarWidth = 220;
    private const int ResultLimit = 200;

    private readonly SearchPlugin _search;
    private readonly IPathActivation _activation;
    private readonly TextBox _queryBox;
    private readonly TextBlock _placeholder;
    private readonly ListBox _typeList;
    private readonly ListBox _modifiedList;
    private readonly ToggleButton _rarelyUsedToggle;
    private readonly ListView _results;
    private readonly StackPanel _emptyPanel;
    private readonly TextBlock _emptyTitle;
    private readonly TextBlock _emptyDetail;
    private readonly TextBlock _indexCountText;
    private readonly Border _rootChrome;
    private readonly Grid _body;
    private readonly Border _sidebar;
    private readonly Border _footer;

    private CancellationTokenSource? _cts;
    private int _version;
    private string _typeCategory = FileTypeCategories.All;
    private string _modifiedId = "all";
    private bool _rarelyUsed;
    private bool _syncing;

    public FileSearchWindow(SearchPlugin search, IPathActivation? activation = null)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _activation = activation ?? new ProcessPathActivation();

        Title = FileSearchChromeText.WindowTitle;
        Width = 960;
        Height = 640;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = WinBoxTheme.SurfaceRaisedBrush;
        Foreground = WinBoxTheme.TextPrimaryBrush;
        FontFamily = WinBoxTheme.UiFont;
        FocusVisualStyle = null;
        WindowIconFactory.Apply(this);

        SourceInitialized += (_, _) =>
        {
            WindowEffects.TryEnableSystemChrome(this, WinBoxTheme.IsDarkEffective);
        };

        var root = new DockPanel { LastChildFill = true };

        _footer = new Border
        {
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10, 16, 10),
            Background = WinBoxTheme.SurfaceRaisedBrush,
        };
        DockPanel.SetDock(_footer, Dock.Bottom);
        var footerRow = new DockPanel { LastChildFill = true };
        _indexCountText = new TextBlock
        {
            Text = FileSearchChromeText.IndexedCount(_search.IndexedCount),
            FontSize = UiLayout.FontFooter,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(_indexCountText, Dock.Left);
        footerRow.Children.Add(_indexCountText);
        footerRow.Children.Add(new TextBlock
        {
            Text = FileSearchChromeText.FooterHints,
            FontSize = UiLayout.FontFooter,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.9,
        });
        _footer.Child = footerRow;
        root.Children.Add(_footer);

        var topBar = new Border
        {
            Padding = new Thickness(16, 14, 16, 12),
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = WinBoxTheme.SurfaceRaisedBrush,
        };
        DockPanel.SetDock(topBar, Dock.Top);
        var queryHost = new Grid();
        _placeholder = new TextBlock
        {
            Text = FileSearchChromeText.Placeholder,
            FontSize = UiLayout.FontInput,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
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
        _queryBox.TextChanged += (_, _) =>
        {
            _placeholder.Visibility = string.IsNullOrEmpty(_queryBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (!_syncing)
            {
                ScheduleSearch();
            }
        };
        queryHost.Children.Add(_placeholder);
        queryHost.Children.Add(_queryBox);
        topBar.Child = queryHost;
        root.Children.Add(topBar);

        _body = new Grid();
        _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SidebarWidth) });
        _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sidebar = new Border
        {
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Background = WinBoxTheme.SurfaceRaisedBrush,
            Padding = new Thickness(10, 12, 10, 12),
        };
        var sidebarScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        ThemedScrollBars.Apply(sidebarScroll);
        var sidebarStack = new StackPanel();

        sidebarStack.Children.Add(CreateSectionHeader(FileSearchChromeText.FilterByHeader));
        _typeList = CreateFilterList(
            FileSearchChromeText.TypeFilters.Select(t => (t.Id, t.Label, t.Glyph)).ToArray(),
            OnTypeSelectionChanged);
        _typeList.SelectedIndex = 0;
        sidebarStack.Children.Add(_typeList);

        sidebarStack.Children.Add(CreateSectionHeader(FileSearchChromeText.RecentlyModifiedHeader));
        _modifiedList = CreateFilterList(
            FileSearchChromeText.ModifiedFilters
                .Select(m => (m.Id, m.Label, "\uE823"))
                .ToArray(),
            OnModifiedSelectionChanged);
        _modifiedList.SelectedIndex = 0;
        sidebarStack.Children.Add(_modifiedList);

        sidebarStack.Children.Add(CreateSectionHeader(FileSearchChromeText.AdvancedHeader));
        _rarelyUsedToggle = new ToggleButton
        {
            Content = FileSearchChromeText.RarelyUsed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = WinBoxTheme.UiFont,
            FontSize = UiLayout.FontSubtitle,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
        };
        _rarelyUsedToggle.Checked += (_, _) =>
        {
            _rarelyUsed = true;
            ScheduleSearch();
        };
        _rarelyUsedToggle.Unchecked += (_, _) =>
        {
            _rarelyUsed = false;
            ScheduleSearch();
        };
        sidebarStack.Children.Add(_rarelyUsedToggle);

        sidebarScroll.Content = sidebarStack;
        _sidebar.Child = sidebarScroll;
        Grid.SetColumn(_sidebar, 0);
        _body.Children.Add(_sidebar);

        var resultsHost = new Grid { Margin = new Thickness(0) };
        resultsHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _results = new ListView
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            FontFamily = WinBoxTheme.UiFont,
            Visibility = Visibility.Collapsed,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_results, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_results, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
        ThemedScrollBars.Apply(_results);
        _results.View = CreateGridView();
        _results.ItemContainerStyle = CreateResultItemStyle();
        _results.MouseDoubleClick += (_, _) => ActivateSelected(reveal: false);
        Grid.SetRow(_results, 0);
        resultsHost.Children.Add(_results);

        _emptyTitle = new TextBlock
        {
            Text = FileSearchChromeText.EmptyTitle,
            FontSize = UiLayout.FontTitle,
            FontWeight = FontWeights.SemiBold,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        _emptyDetail = new TextBlock
        {
            Text = FileSearchChromeText.EmptyDetail,
            FontSize = UiLayout.FontSubtitle,
            FontFamily = WinBoxTheme.UiFont,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            Margin = new Thickness(0, 6, 0, 0),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        _emptyPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(32),
            MaxWidth = 360,
        };
        _emptyPanel.Children.Add(_emptyTitle);
        _emptyPanel.Children.Add(_emptyDetail);
        Grid.SetRow(_emptyPanel, 0);
        resultsHost.Children.Add(_emptyPanel);

        Grid.SetColumn(resultsHost, 1);
        _body.Children.Add(resultsHost);
        root.Children.Add(_body);

        _rootChrome = new Border { Child = root, Background = WinBoxTheme.SurfaceRaisedBrush };
        Content = _rootChrome;

        PreviewKeyDown += OnPreviewKeyDown;
        WinBoxTheme.Changed += OnThemeChanged;
        UiLayout.Changed += OnLayoutChanged;
        Closed += (_, _) =>
        {
            WinBoxTheme.Changed -= OnThemeChanged;
            UiLayout.Changed -= OnLayoutChanged;
            _cts?.Cancel();
            _cts?.Dispose();
        };

        ShowIdleEmpty();
    }

    public void Open(string? seedQuery = null)
    {
        _indexCountText.Text = FileSearchChromeText.IndexedCount(_search.IndexedCount);

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;

        _syncing = true;
        _queryBox.Text = seedQuery ?? string.Empty;
        _syncing = false;
        _placeholder.Visibility = string.IsNullOrEmpty(_queryBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _queryBox.Focus();
        _queryBox.SelectAll();
        ScheduleSearch();
    }

    private void OnTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_typeList.SelectedItem is FilterRow row)
        {
            _typeCategory = row.Id;
            ScheduleSearch();
        }
    }

    private void OnModifiedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_modifiedList.SelectedItem is FilterRow row)
        {
            _modifiedId = row.Id;
            ScheduleSearch();
        }
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

            var text = _queryBox.Text ?? string.Empty;
            var extensions = FileTypeCategories.ExtensionsFor(_typeCategory);
            DateTime? modifiedAfter = null;
            var mod = FileSearchChromeText.ModifiedFilters.FirstOrDefault(m => m.Id == _modifiedId);
            if (mod.Days is int days)
            {
                modifiedAfter = DateTime.UtcNow.AddDays(-days);
            }

            var hasConstraint = !string.IsNullOrWhiteSpace(text)
                || extensions is { Count: > 0 }
                || modifiedAfter is not null
                || _rarelyUsed;

            if (!hasConstraint)
            {
                ShowIdleEmpty();
                return;
            }

            var query = new SearchQuery(
                Text: text,
                Extensions: extensions,
                ModifiedAfterUtc: modifiedAfter,
                RarelyUsedOnly: _rarelyUsed,
                Limit: ResultLimit);

            var hits = await _search.SearchAsync(query, token).ConfigureAwait(true);
            if (version != Volatile.Read(ref _version) || token.IsCancellationRequested)
            {
                return;
            }

            ApplyResults(hits, text);
            _indexCountText.Text = FileSearchChromeText.IndexedCount(_search.IndexedCount);
        }
        catch (OperationCanceledException)
        {
            // newer keystroke / filter
        }
    }

    private void ApplyResults(IReadOnlyList<SearchHit> hits, string queryText)
    {
        if (hits.Count == 0)
        {
            _results.ItemsSource = null;
            _results.Visibility = Visibility.Collapsed;
            _emptyTitle.Text = FileSearchChromeText.NoResultsTitle;
            _emptyDetail.Text = string.IsNullOrWhiteSpace(queryText)
                ? FileSearchChromeText.NoResultsDetail
                : LauncherChromeText.NoResultsTitle(queryText);
            _emptyPanel.Visibility = Visibility.Visible;
            return;
        }

        _emptyPanel.Visibility = Visibility.Collapsed;
        _results.Visibility = Visibility.Visible;
        _results.ItemsSource = hits.Select(ResultRow.FromHit).ToList();
        _results.SelectedIndex = 0;
    }

    private void ShowIdleEmpty()
    {
        _results.ItemsSource = null;
        _results.Visibility = Visibility.Collapsed;
        _emptyTitle.Text = FileSearchChromeText.EmptyTitle;
        _emptyDetail.Text = FileSearchChromeText.EmptyDetail;
        _emptyPanel.Visibility = Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (key == Key.Down)
        {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (key == Key.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (key == Key.Enter)
        {
            var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            ActivateSelected(reveal: alt);
            e.Handled = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_results.Items.Count == 0)
        {
            return;
        }

        var next = Math.Clamp(_results.SelectedIndex + delta, 0, _results.Items.Count - 1);
        _results.SelectedIndex = next;
        _results.ScrollIntoView(_results.SelectedItem);
    }

    private void ActivateSelected(bool reveal)
    {
        if (_results.SelectedItem is not ResultRow row || string.IsNullOrWhiteSpace(row.Path))
        {
            return;
        }

        if (reveal)
        {
            _activation.RevealInFolder(row.Path);
        }
        else
        {
            _activation.Open(row.Path);
        }
    }

    private void OnThemeChanged()
    {
        Dispatcher.Invoke(() =>
        {
            Background = WinBoxTheme.SurfaceRaisedBrush;
            Foreground = WinBoxTheme.TextPrimaryBrush;
            _rootChrome.Background = WinBoxTheme.SurfaceRaisedBrush;
            _sidebar.Background = WinBoxTheme.SurfaceRaisedBrush;
            _sidebar.BorderBrush = WinBoxTheme.BorderSubtleBrush;
            _footer.Background = WinBoxTheme.SurfaceRaisedBrush;
            _footer.BorderBrush = WinBoxTheme.BorderSubtleBrush;
            _queryBox.Foreground = WinBoxTheme.TextPrimaryBrush;
            _queryBox.CaretBrush = WinBoxTheme.AccentBrush;
            _placeholder.Foreground = WinBoxTheme.TextSecondaryBrush;
            _results.Foreground = WinBoxTheme.TextPrimaryBrush;
            _results.ItemContainerStyle = CreateResultItemStyle();
            _emptyTitle.Foreground = WinBoxTheme.TextPrimaryBrush;
            _emptyDetail.Foreground = WinBoxTheme.TextSecondaryBrush;
            _indexCountText.Foreground = WinBoxTheme.TextSecondaryBrush;
            _rarelyUsedToggle.Foreground = WinBoxTheme.TextPrimaryBrush;
            WindowEffects.TryEnableSystemChrome(this, WinBoxTheme.IsDarkEffective);
        });
    }

    private void OnLayoutChanged()
    {
        Dispatcher.Invoke(() =>
        {
            _queryBox.FontSize = UiLayout.FontInput;
            _placeholder.FontSize = UiLayout.FontInput;
            _emptyTitle.FontSize = UiLayout.FontTitle;
            _emptyDetail.FontSize = UiLayout.FontSubtitle;
            _indexCountText.FontSize = UiLayout.FontFooter;
            ScrollViewer.SetVerticalScrollBarVisibility(_results, UiLayout.ToVisibility(UiLayout.ScrollBarMode));
            ThemedScrollBars.Apply(_results);
        });
    }

    private static TextBlock CreateSectionHeader(string text) => new()
    {
        Text = text,
        FontSize = UiLayout.FontFooter,
        FontWeight = FontWeights.SemiBold,
        Foreground = WinBoxTheme.TextSecondaryBrush,
        Margin = new Thickness(8, 10, 8, 6),
        Opacity = 0.95,
    };

    private static ListBox CreateFilterList(
        IReadOnlyList<(string Id, string Label, string Glyph)> items,
        SelectionChangedEventHandler onChanged)
    {
        var list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = WinBoxTheme.UiFont,
            FontSize = UiLayout.FontSubtitle,
            Margin = new Thickness(0, 0, 0, 8),
            FocusVisualStyle = null,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        list.ItemContainerStyle = CreateFilterItemStyle();
        list.ItemTemplate = CreateFilterItemTemplate();
        foreach (var item in items)
        {
            list.Items.Add(new FilterRow(item.Id, item.Label, item.Glyph));
        }

        list.SelectionChanged += onChanged;
        return list;
    }

    private static DataTemplate CreateFilterItemTemplate()
    {
        var template = new DataTemplate(typeof(FilterRow));
        var factory = new FrameworkElementFactory(typeof(DockPanel));
        factory.SetValue(DockPanel.LastChildFillProperty, true);

        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FilterRow.Glyph)));
        glyph.SetValue(TextBlock.FontFamilyProperty, WinBoxTheme.GlyphFont);
        glyph.SetValue(TextBlock.FontSizeProperty, 12.0);
        glyph.SetValue(TextBlock.ForegroundProperty, WinBoxTheme.TextSecondaryBrush);
        glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        glyph.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
        glyph.SetValue(DockPanel.DockProperty, Dock.Left);
        factory.AppendChild(glyph);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FilterRow.Label)));
        label.SetValue(TextBlock.ForegroundProperty, WinBoxTheme.TextPrimaryBrush);
        label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        factory.AppendChild(label);

        template.VisualTree = factory;
        return template;
    }

    private static Style CreateFilterItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 7, 8, 7)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateFilterItemTemplateControl()));
        return style;
    }

    private static ControlTemplate CreateFilterItemTemplateControl()
    {
        var template = new ControlTemplate(typeof(ListBoxItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(3, 0, 0, 0));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;

        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, WinBoxTheme.SelectionBrush) { TargetName = "Bd" });
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, WinBoxTheme.AccentBrush) { TargetName = "Bd" });
        template.Triggers.Add(selected);

        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(ListBoxItem.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
        hover.Setters.Add(new Setter(Border.BackgroundProperty, WinBoxTheme.HoverBrush) { TargetName = "Bd" });
        template.Triggers.Add(hover);

        return template;
    }

    private static GridView CreateGridView()
    {
        var nameCol = new GridViewColumn
        {
            Header = FileSearchChromeText.ColName,
            Width = 220,
            CellTemplate = CreateNameCellTemplate(),
        };
        var pathCol = new GridViewColumn
        {
            Header = FileSearchChromeText.ColPath,
            Width = 360,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ResultRow.Path)),
        };
        var dateCol = new GridViewColumn
        {
            Header = FileSearchChromeText.ColModified,
            Width = 140,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ResultRow.ModifiedDisplay)),
        };

        return new GridView { Columns = { nameCol, pathCol, dateCol } };
    }

    private static DataTemplate CreateNameCellTemplate()
    {
        var template = new DataTemplate(typeof(ResultRow));
        var factory = new FrameworkElementFactory(typeof(DockPanel));
        factory.SetValue(DockPanel.LastChildFillProperty, true);

        var icon = new FrameworkElementFactory(typeof(Image));
        icon.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(ResultRow.Icon)));
        icon.SetValue(Image.WidthProperty, WinBoxTheme.ResultIconSize);
        icon.SetValue(Image.HeightProperty, WinBoxTheme.ResultIconSize);
        icon.SetValue(Image.MarginProperty, new Thickness(0, 0, 8, 0));
        icon.SetValue(Image.StretchProperty, Stretch.Uniform);
        icon.SetValue(DockPanel.DockProperty, Dock.Left);
        factory.AppendChild(icon);

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ResultRow.Name)));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(name);

        template.VisualTree = factory;
        return template;
    }

    private static Style CreateResultItemStyle()
    {
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

        var selected = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, WinBoxTheme.SelectionBrush));
        style.Triggers.Add(selected);

        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(ListViewItem.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(ListViewItem.IsSelectedProperty, false));
        hover.Setters.Add(new Setter(Control.BackgroundProperty, WinBoxTheme.HoverBrush));
        style.Triggers.Add(hover);

        return style;
    }

    private sealed record FilterRow(string Id, string Label, string Glyph);

    private sealed class ResultRow
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required string ModifiedDisplay { get; init; }
        public ImageSource? Icon { get; init; }

        public static ResultRow FromHit(SearchHit hit) => new()
        {
            Name = hit.Name,
            Path = hit.Path,
            ModifiedDisplay = hit.LastWriteTimeUtc is { } utc
                ? utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : string.Empty,
            Icon = ShellFileIcons.GetForPath(hit.Path),
        };
    }
}
