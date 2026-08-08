using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinBox.Search;
using WinBox.Search.Index;

namespace WinBox.Host.Ui;

/// <summary>
/// Host settings: Index scope, General (theme), Shortcuts reference.
/// </summary>
internal sealed class IndexSettingsWindow : Window
{
    private readonly SearchPlugin _search;
    private readonly IndexOptionsStore _indexStore;
    private readonly UiOptionsStore _uiStore;
    private readonly ListBox _rootsList;
    private readonly ListBox _excludeRootsList;
    private readonly TextBox _includeExtensionsBox;
    private readonly TextBox _excludeExtensionsBox;
    private readonly TextBox _excludePatternsBox;
    private readonly CheckBox _recursiveBox;
    private readonly ComboBox _themeBox;
    private readonly Slider _widthSlider;
    private readonly Slider _resultsHeightSlider;
    private readonly Slider _fontInputSlider;
    private readonly Slider _fontTitleSlider;
    private readonly Slider _scrollWidthSlider;
    private readonly ComboBox _scrollModeBox;
    private readonly TextBlock _widthValue;
    private readonly TextBlock _resultsHeightValue;
    private readonly TextBlock _fontInputValue;
    private readonly TextBlock _fontTitleValue;
    private readonly TextBlock _scrollWidthValue;
    private readonly TextBlock _statusText;
    private readonly Button _saveButton;
    private readonly TabControl _tabs;
    private bool _loadingAppearance;

    public IndexSettingsWindow(
        SearchPlugin search,
        IndexOptionsStore indexStore,
        UiOptionsStore uiStore,
        SettingsTab initialTab = SettingsTab.Index)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
        _uiStore = uiStore ?? throw new ArgumentNullException(nameof(uiStore));

        Title = "WinBox — Settings";
        Width = 640;
        Height = 720;
        MinWidth = 520;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = WinBoxTheme.SurfaceRaisedBrush;
        Foreground = WinBoxTheme.TextPrimaryBrush;
        FontFamily = WinBoxTheme.UiFont;

        SourceInitialized += (_, _) =>
        {
            var dark = WinBoxTheme.Resolve(WinBoxTheme.CurrentKind).TextPrimary.R > 0x80;
            WindowEffects.TryEnableSystemChrome(this, dark);
        };

        var root = new DockPanel { Margin = new Thickness(20) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        _saveButton = CreateButton("Save & rebuild", primary: true, minWidth: 128);
        _saveButton.Click += async (_, _) => await SaveAndRebuildAsync().ConfigureAwait(true);

        var closeButton = CreateButton("Close", primary: false, minWidth: 88);
        closeButton.IsCancel = true;
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(_saveButton);
        buttons.Children.Add(closeButton);
        root.Children.Add(buttons);

        _statusText = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = WinBoxTheme.TextSecondaryBrush,
            TextWrapping = TextWrapping.Wrap,
            FontSize = WinBoxTheme.FontSubtitle,
        };
        DockPanel.SetDock(_statusText, Dock.Bottom);
        root.Children.Add(_statusText);

        _tabs = new TabControl
        {
            Background = WinBoxTheme.SurfaceRaisedBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            Padding = new Thickness(0, 8, 0, 0),
        };

        var indexForm = new StackPanel();
        indexForm.Children.Add(SectionLabel("Index roots"));
        indexForm.Children.Add(Hint("Folders to scan. Start broad; tighten with excludes below."));
        _rootsList = PathListBox();
        indexForm.Children.Add(_rootsList);
        indexForm.Children.Add(PathListButtons(
            add: () => AddFolderToList(_rootsList, "Choose a folder to index"),
            remove: () => RemoveSelected(_rootsList)));

        indexForm.Children.Add(SectionLabel("Exclude roots"));
        indexForm.Children.Add(Hint("Skip these folders (and everything under them), even if inside an index root."));
        _excludeRootsList = PathListBox(height: 88);
        indexForm.Children.Add(_excludeRootsList);
        indexForm.Children.Add(PathListButtons(
            add: () => AddFolderToList(_excludeRootsList, "Choose a folder to exclude"),
            remove: () => RemoveSelected(_excludeRootsList)));

        indexForm.Children.Add(SectionLabel("Include extensions"));
        indexForm.Children.Add(Hint("Empty = all types. Comma-separated, e.g. md, go, txt"));
        _includeExtensionsBox = FieldBox();
        indexForm.Children.Add(_includeExtensionsBox);

        indexForm.Children.Add(SectionLabel("Exclude extensions"));
        indexForm.Children.Add(Hint("Always skipped. Wins over include list. e.g. exe, dll, obj"));
        _excludeExtensionsBox = FieldBox();
        indexForm.Children.Add(_excludeExtensionsBox);

        indexForm.Children.Add(SectionLabel("Exclude path patterns"));
        indexForm.Children.Add(Hint("Skip when any path segment equals the name (one per line). e.g. node_modules, .git"));
        _excludePatternsBox = FieldBox(height: 96, acceptReturn: true);
        indexForm.Children.Add(_excludePatternsBox);

        _recursiveBox = new CheckBox
        {
            Content = "Scan subfolders recursively",
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = WinBoxTheme.TextPrimaryBrush,
            IsChecked = true,
        };
        indexForm.Children.Add(_recursiveBox);

        _tabs.Items.Add(CreateTab("Index", WrapScroll(indexForm)));

        var appearance = new StackPanel();
        appearance.Children.Add(SectionLabel("Theme"));
        appearance.Children.Add(Hint("Applies immediately to the launcher and this window."));
        _themeBox = new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 12),
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = WinBoxTheme.SurfaceSunkenBrush,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            Padding = new Thickness(8, 6, 8, 6),
        };
        _themeBox.Items.Add(new ComboBoxItem { Content = "Dark", Tag = UiThemeKind.Dark });
        _themeBox.Items.Add(new ComboBoxItem { Content = "Light", Tag = UiThemeKind.Light });
        _themeBox.Items.Add(new ComboBoxItem { Content = "System", Tag = UiThemeKind.System });
        _themeBox.SelectionChanged += (_, _) => PersistAppearanceFromUi();
        appearance.Children.Add(_themeBox);

        appearance.Children.Add(SectionLabel("Launcher size"));
        (_widthSlider, _widthValue) = LabeledSlider(appearance, "Width", 400, 900, 10);
        (_resultsHeightSlider, _resultsHeightValue) = LabeledSlider(appearance, "Results max height", 160, 560, 10);

        appearance.Children.Add(SectionLabel("Typography"));
        (_fontInputSlider, _fontInputValue) = LabeledSlider(appearance, "Input font", 14, 28, 1);
        (_fontTitleSlider, _fontTitleValue) = LabeledSlider(appearance, "Result title font", 11, 20, 1);

        appearance.Children.Add(SectionLabel("Scrollbar"));
        appearance.Children.Add(Hint("Auto = only when results overflow. Hidden = wheel/keys still scroll."));
        (_scrollWidthSlider, _scrollWidthValue) = LabeledSlider(appearance, "Thickness", 4, 16, 1);
        _scrollModeBox = new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 8),
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = WinBoxTheme.SurfaceSunkenBrush,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            Padding = new Thickness(8, 6, 8, 6),
        };
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Auto (only when needed)", Tag = ScrollBarShowMode.Auto });
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Hidden", Tag = ScrollBarShowMode.Hidden });
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Always", Tag = ScrollBarShowMode.Always });
        _scrollModeBox.SelectionChanged += (_, _) => PersistAppearanceFromUi();
        appearance.Children.Add(_scrollModeBox);

        appearance.Children.Add(Hint($"Saved to {_uiStore.FilePath}"));
        _tabs.Items.Add(CreateTab("Appearance", WrapScroll(appearance)));

        var shortcuts = new StackPanel();
        shortcuts.Children.Add(SectionLabel("Launcher"));
        shortcuts.Children.Add(ShortcutRow("Shift+Alt+U", "Open launcher"));
        shortcuts.Children.Add(ShortcutRow("Esc", "Dismiss launcher"));
        shortcuts.Children.Add(ShortcutRow("↑ / ↓", "Move selection"));
        shortcuts.Children.Add(ShortcutRow("Enter", "Activate selected result"));
        shortcuts.Children.Add(ShortcutRow("Alt+Enter", "Reveal path in Explorer"));
        shortcuts.Children.Add(SectionLabel("Tray"));
        shortcuts.Children.Add(ShortcutRow("Double-click", "Open launcher"));
        shortcuts.Children.Add(ShortcutRow("Right-click", "Settings / Quit"));
        shortcuts.Children.Add(Hint("Custom hotkeys are not editable yet — tracked for a later release."));
        _tabs.Items.Add(CreateTab("Shortcuts", shortcuts));

        _tabs.SelectionChanged += (_, _) =>
        {
            _saveButton.Visibility = _tabs.SelectedIndex == (int)SettingsTab.Index
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        root.Children.Add(_tabs);
        Content = root;

        LoadFromOptions(_search.Options);
        LoadAppearanceFromStore();
        _tabs.SelectedIndex = (int)initialTab;
        _saveButton.Visibility = initialTab == SettingsTab.Index ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus(initialTab == SettingsTab.Appearance
            ? $"Appearance · {_uiStore.FilePath}"
            : $"Index config: {_indexStore.FilePath}");
        WinBoxTheme.Changed += OnHostThemeChanged;
        Closed += (_, _) => WinBoxTheme.Changed -= OnHostThemeChanged;
    }

    public void ShowTab(SettingsTab tab)
    {
        _tabs.SelectedIndex = (int)tab;
        BringIntoView();
    }

    private (Slider slider, TextBlock value) LabeledSlider(
        Panel parent,
        string label,
        double min,
        double max,
        double tick)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            Margin = new Thickness(0, 4, 0, 2),
        });
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var value = new TextBlock
        {
            Width = 40,
            TextAlignment = TextAlignment.Right,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(value, Dock.Right);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = tick,
            IsSnapToTickEnabled = tick >= 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.ValueChanged += (_, _) =>
        {
            value.Text = Math.Round(slider.Value).ToString();
            PersistAppearanceFromUi();
        };
        row.Children.Add(value);
        row.Children.Add(slider);
        parent.Children.Add(row);
        return (slider, value);
    }

    private void LoadAppearanceFromStore()
    {
        _loadingAppearance = true;
        try
        {
            var options = _uiStore.LoadOrDefault();
            SelectTheme(WinBoxTheme.ParseTheme(options.Theme));
            SetSlider(_widthSlider, _widthValue, options.OverlayWidth);
            SetSlider(_resultsHeightSlider, _resultsHeightValue, options.ResultsMaxHeight);
            SetSlider(_fontInputSlider, _fontInputValue, options.FontInput);
            SetSlider(_fontTitleSlider, _fontTitleValue, options.FontTitle);
            SetSlider(_scrollWidthSlider, _scrollWidthValue, options.ScrollBarWidth);
            SelectScrollMode(UiLayout.ParseScrollBarMode(options.ScrollBarMode));
        }
        finally
        {
            _loadingAppearance = false;
        }
    }

    private static void SetSlider(Slider slider, TextBlock label, double value)
    {
        slider.Value = value;
        label.Text = Math.Round(value).ToString();
    }

    private void SelectTheme(UiThemeKind kind)
    {
        foreach (ComboBoxItem item in _themeBox.Items)
        {
            if (item.Tag is UiThemeKind tagged && tagged == kind)
            {
                _themeBox.SelectedItem = item;
                return;
            }
        }

        _themeBox.SelectedIndex = 0;
    }

    private void SelectScrollMode(ScrollBarShowMode mode)
    {
        foreach (ComboBoxItem item in _scrollModeBox.Items)
        {
            if (item.Tag is ScrollBarShowMode tagged && tagged == mode)
            {
                _scrollModeBox.SelectedItem = item;
                return;
            }
        }

        _scrollModeBox.SelectedIndex = 0;
    }

    private void PersistAppearanceFromUi()
    {
        if (_loadingAppearance)
        {
            return;
        }

        var options = _uiStore.LoadOrDefault();
        if (_themeBox.SelectedItem is ComboBoxItem { Tag: UiThemeKind theme })
        {
            options.Theme = WinBoxTheme.ToStorage(theme);
            WinBoxTheme.Apply(theme);
        }

        options.OverlayWidth = _widthSlider.Value;
        options.ResultsMaxHeight = _resultsHeightSlider.Value;
        options.FontInput = _fontInputSlider.Value;
        options.FontTitle = _fontTitleSlider.Value;
        options.FontSubtitle = Math.Max(10, _fontTitleSlider.Value - 2);
        options.ScrollBarWidth = _scrollWidthSlider.Value;
        if (_scrollModeBox.SelectedItem is ComboBoxItem { Tag: ScrollBarShowMode mode })
        {
            options.ScrollBarMode = UiLayout.ToStorage(mode);
        }

        try
        {
            _uiStore.Save(options);
            UiLayout.Apply(options);
            var dark = WinBoxTheme.Resolve(WinBoxTheme.ParseTheme(options.Theme)).TextPrimary.R > 0x80;
            WindowEffects.TryEnableSystemChrome(this, dark);
            Background = WinBoxTheme.SurfaceRaisedBrush;
            Foreground = WinBoxTheme.TextPrimaryBrush;
            UpdateStatus("Appearance saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UpdateStatus($"Appearance save failed: {ex.Message}");
        }
    }

    private void OnHostThemeChanged()
    {
        Dispatcher.Invoke(() =>
        {
            Background = WinBoxTheme.SurfaceRaisedBrush;
            Foreground = WinBoxTheme.TextPrimaryBrush;
            _statusText.Foreground = WinBoxTheme.TextSecondaryBrush;
        });
    }

    private void LoadFromOptions(IndexOptions options)
    {
        FillList(_rootsList, options.Roots);
        FillList(_excludeRootsList, options.ExcludeRoots);
        _includeExtensionsBox.Text = IndexOptionsText.JoinComma(options.IncludeExtensions);
        _excludeExtensionsBox.Text = IndexOptionsText.JoinComma(options.ExcludeExtensions);
        _excludePatternsBox.Text = IndexOptionsText.JoinLines(options.ExcludePathPatterns);
        _recursiveBox.IsChecked = options.Recursive;
    }

    private IndexOptions CaptureOptions()
    {
        var excludePatterns = IndexOptionsText.SplitList(_excludePatternsBox.Text, '\n', '\r');

        return new IndexOptions
        {
            Roots = _rootsList.Items.Cast<string>().ToArray(),
            ExcludeRoots = _excludeRootsList.Items.Cast<string>().ToArray(),
            IncludeExtensions = IndexOptionsText.SplitExtensions(_includeExtensionsBox.Text),
            ExcludeExtensions = IndexOptionsText.SplitExtensions(_excludeExtensionsBox.Text),
            IncludePathPatterns = _search.Options.IncludePathPatterns.ToArray(),
            ExcludePathPatterns = excludePatterns.Count > 0
                ? excludePatterns
                : IndexOptions.DefaultExcludePathPatterns,
            Recursive = _recursiveBox.IsChecked == true,
        };
    }

    private void AddFolderToList(ListBox list, string dialogTitle)
    {
        var dialog = new OpenFolderDialog { Title = dialogTitle };
        if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        var path = dialog.FolderName;
        foreach (var existing in list.Items.Cast<string>())
        {
            if (existing.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Items.Add(path);
    }

    private static void RemoveSelected(ListBox list)
    {
        if (list.SelectedItem is string selected)
        {
            list.Items.Remove(selected);
        }
    }

    private static void FillList(ListBox list, IEnumerable<string> values)
    {
        list.Items.Clear();
        foreach (var value in values)
        {
            list.Items.Add(value);
        }
    }

    private async Task SaveAndRebuildAsync()
    {
        try
        {
            var options = CaptureOptions();
            _indexStore.Save(options);
            UpdateStatus("Rebuilding index…");

            await _search.ApplyOptionsAsync(options).ConfigureAwait(true);

            UpdateStatus($"Saved. Indexed {_search.IndexedCount} file(s).");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Save failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "WinBox settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStatus(string text) => _statusText.Text = text;

    private static TabItem CreateTab(string header, UIElement content) => new()
    {
        Header = header,
        Content = content,
        Padding = new Thickness(12, 6, 12, 6),
    };

    private static ScrollViewer WrapScroll(UIElement content)
    {
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        ThemedScrollBars.Apply(scroll);
        return scroll;
    }

    private static UIElement ShortcutRow(string keys, string description)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var key = new TextBlock
        {
            Text = keys,
            Width = 120,
            FontWeight = FontWeights.SemiBold,
            Foreground = WinBoxTheme.AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(key, Dock.Left);
        row.Children.Add(key);
        row.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        return row;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = WinBoxTheme.FontTitle,
        Margin = new Thickness(0, 12, 0, 2),
        Foreground = WinBoxTheme.TextPrimaryBrush,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = WinBoxTheme.FontSubtitle,
        Foreground = WinBoxTheme.TextSecondaryBrush,
        Margin = new Thickness(0, 0, 0, 6),
        TextWrapping = TextWrapping.Wrap,
    };

    private static ListBox PathListBox(double height = 100)
    {
        var list = new ListBox
        {
            Height = height,
            Background = WinBoxTheme.SurfaceSunkenBrush,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        ThemedScrollBars.Apply(list);

        var template = new DataTemplate(typeof(string));
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        textFactory.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding());
        textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        textFactory.SetValue(TextBlock.PaddingProperty, new Thickness(6, 3, 6, 3));
        template.VisualTree = textFactory;
        list.ItemTemplate = template;
        return list;
    }

    private static StackPanel PathListButtons(Action add, Action remove)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 4),
        };
        var addButton = CreateButton("Add folder…", primary: false);
        addButton.Click += (_, _) => add();
        var removeButton = CreateButton("Remove", primary: false);
        removeButton.Margin = new Thickness(8, 0, 0, 0);
        removeButton.Click += (_, _) => remove();
        row.Children.Add(addButton);
        row.Children.Add(removeButton);
        return row;
    }

    private static TextBox FieldBox(double? height = null, bool acceptReturn = false)
    {
        var box = new TextBox
        {
            AcceptsReturn = acceptReturn,
            TextWrapping = acceptReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = acceptReturn ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = WinBoxTheme.SurfaceSunkenBrush,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            CaretBrush = WinBoxTheme.TextPrimaryBrush,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 4),
            FontFamily = WinBoxTheme.UiFont,
        };

        if (height is not null)
        {
            box.Height = height.Value;
        }

        return box;
    }

    private static Button CreateButton(string content, bool primary, double minWidth = 0)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = primary ? new Thickness(0, 0, 8, 0) : new Thickness(0),
            MinWidth = minWidth,
            FontFamily = WinBoxTheme.UiFont,
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(1),
        };

        if (primary)
        {
            button.Background = WinBoxTheme.PrimaryButtonBrush;
            button.Foreground = Brushes.White;
            button.BorderBrush = WinBoxTheme.PrimaryButtonBrush;
        }
        else
        {
            button.Background = WinBoxTheme.SurfaceSunkenBrush;
            button.Foreground = WinBoxTheme.TextPrimaryBrush;
            button.BorderBrush = WinBoxTheme.BorderSubtleBrush;
        }

        return button;
    }
}
