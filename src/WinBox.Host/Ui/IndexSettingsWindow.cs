using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinBox.Search;
using WinBox.Search.Index;

namespace WinBox.Host.Ui;

/// <summary>
/// Host settings: General (startup), Index scope, Appearance, Shortcuts.
/// </summary>
internal sealed class IndexSettingsWindow : Window
{
    private readonly SearchPlugin _search;
    private readonly IndexOptionsStore _indexStore;
    private readonly UiOptionsStore _uiStore;
    private readonly LoginAutoStart _loginAutoStart;
    private readonly ListBox _rootsList;
    private readonly ListBox _excludeRootsList;
    private readonly TextBox _includeExtensionsBox;
    private readonly TextBox _excludeExtensionsBox;
    private readonly TextBox _excludePatternsBox;
    private readonly CheckBox _recursiveBox;
    private readonly CheckBox _startWithWindowsBox;
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
    private readonly Border _footerBar;
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
        _loginAutoStart = new LoginAutoStart();

        Title = "WinBox — Settings";
        Width = 660;
        Height = 720;
        MinWidth = 540;
        MinHeight = 520;
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

        var root = new DockPanel { Margin = new Thickness(WinBoxTheme.SettingsPageMargin) };

        _footerBar = new Border
        {
            Tag = SettingsChrome.FieldCardTag,
            Background = Brushes.Transparent,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0, 14, 0, 0),
            Margin = new Thickness(0, 12, 0, 0),
            Effect = null,
        };
        DockPanel.SetDock(_footerBar, Dock.Bottom);

        var footerInner = new DockPanel();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(buttons, Dock.Right);

        _saveButton = CreateButton("Save & rebuild", primary: true, minWidth: 128);
        _saveButton.Click += async (_, _) => await SaveAndRebuildAsync().ConfigureAwait(true);

        var closeButton = CreateButton("Close", primary: false, minWidth: 88);
        closeButton.IsCancel = true;
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(_saveButton);
        buttons.Children.Add(closeButton);
        footerInner.Children.Add(buttons);

        _statusText = new TextBlock
        {
            Foreground = WinBoxTheme.TextSecondaryBrush,
            TextWrapping = TextWrapping.Wrap,
            FontSize = WinBoxTheme.FontSubtitle,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Tag = "hint",
        };
        footerInner.Children.Add(_statusText);
        _footerBar.Child = footerInner;
        root.Children.Add(_footerBar);

        _tabs = new TabControl();
        SettingsChrome.ApplyTabControl(_tabs);

        var general = new StackPanel();
        general.Children.Add(SectionLabel("Startup", first: true));
        general.Children.Add(Hint("Registers WinBox under your Windows sign-in (HKCU Run). No admin required."));
        _startWithWindowsBox = new CheckBox
        {
            Content = "Start WinBox when I sign in",
            Margin = new Thickness(2, 4, 0, 4),
            Foreground = WinBoxTheme.TextPrimaryBrush,
            FontFamily = WinBoxTheme.UiFont,
            FocusVisualStyle = null,
        };
        _startWithWindowsBox.Checked += (_, _) => PersistGeneralFromUi();
        _startWithWindowsBox.Unchecked += (_, _) => PersistGeneralFromUi();
        general.Children.Add(_startWithWindowsBox);
        general.Children.Add(Hint("After moving WinBox, turn this off and on again to refresh the path."));
        _tabs.Items.Add(CreateTab("General", WrapScroll(general)));

        var indexForm = new StackPanel();
        indexForm.Children.Add(SectionLabel("Index roots", first: true));
        indexForm.Children.Add(Hint("Folders to scan. Start broad; tighten with excludes below."));
        _rootsList = SettingsChrome.CreatePathList(emptyRows: 1);
        indexForm.Children.Add(SettingsChrome.WrapFlat(_rootsList));
        indexForm.Children.Add(PathListButtons(
            add: () => AddFolderToList(_rootsList, "Choose a folder to index"),
            remove: () => RemoveSelected(_rootsList)));

        indexForm.Children.Add(SectionLabel("Exclude roots"));
        indexForm.Children.Add(Hint("Skip these folders (and everything under them), even if inside an index root."));
        _excludeRootsList = SettingsChrome.CreatePathList(emptyRows: 1);
        indexForm.Children.Add(SettingsChrome.WrapFlat(_excludeRootsList));
        indexForm.Children.Add(PathListButtons(
            add: () => AddFolderToList(_excludeRootsList, "Choose a folder to exclude"),
            remove: () => RemoveSelected(_excludeRootsList)));

        indexForm.Children.Add(SectionLabel("Include extensions"));
        indexForm.Children.Add(Hint("Empty = all types. Comma-separated, e.g. md, go, txt"));
        _includeExtensionsBox = SettingsChrome.CreateField();
        indexForm.Children.Add(SettingsChrome.WrapFlat(_includeExtensionsBox));

        indexForm.Children.Add(SectionLabel("Exclude extensions"));
        indexForm.Children.Add(Hint("Always skipped. Wins over include list. e.g. exe, dll, obj"));
        _excludeExtensionsBox = SettingsChrome.CreateField();
        indexForm.Children.Add(SettingsChrome.WrapFlat(_excludeExtensionsBox));

        indexForm.Children.Add(SectionLabel("Exclude path patterns"));
        indexForm.Children.Add(Hint("Skip when any path segment equals the name (one per line). e.g. node_modules, .git"));
        _excludePatternsBox = SettingsChrome.CreateField(height: 88, acceptReturn: true);
        indexForm.Children.Add(SettingsChrome.WrapFlat(_excludePatternsBox));

        _recursiveBox = new CheckBox
        {
            Content = "Scan subfolders recursively",
            Margin = new Thickness(2, WinBoxTheme.SettingsSectionGap, 0, 4),
            Foreground = WinBoxTheme.TextPrimaryBrush,
            IsChecked = true,
            FontFamily = WinBoxTheme.UiFont,
            FocusVisualStyle = null,
        };
        indexForm.Children.Add(_recursiveBox);

        _tabs.Items.Add(CreateTab("Index", WrapScroll(indexForm)));

        var appearance = new StackPanel();
        appearance.Children.Add(SectionLabel("Theme", first: true));
        appearance.Children.Add(Hint("Applies immediately to the launcher and this window."));
        _themeBox = new ComboBox
        {
            Margin = new Thickness(0, 2, 0, 8),
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontFamily = WinBoxTheme.UiFont,
        };
        SettingsChrome.StyleCombo(_themeBox);
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
            Margin = new Thickness(0, 8, 0, 4),
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontFamily = WinBoxTheme.UiFont,
        };
        SettingsChrome.StyleCombo(_scrollModeBox);
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Auto (only when needed)", Tag = ScrollBarShowMode.Auto });
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Hidden", Tag = ScrollBarShowMode.Hidden });
        _scrollModeBox.Items.Add(new ComboBoxItem { Content = "Always", Tag = ScrollBarShowMode.Always });
        _scrollModeBox.SelectionChanged += (_, _) => PersistAppearanceFromUi();
        appearance.Children.Add(_scrollModeBox);

        appearance.Children.Add(Hint($"Saved to {_uiStore.FilePath}"));
        _tabs.Items.Add(CreateTab("Appearance", WrapScroll(appearance)));

        var shortcuts = new StackPanel();
        shortcuts.Children.Add(SectionLabel("Launcher", first: true));
        shortcuts.Children.Add(ShortcutRows(
            ("Shift+Alt+U", "Open launcher"),
            ("Esc", "Dismiss launcher"),
            ("↑ / ↓", "Move selection"),
            ("Enter", "Activate selected result"),
            ("Alt+Enter", "Reveal path in Explorer")));
        shortcuts.Children.Add(SectionLabel("Tray"));
        shortcuts.Children.Add(ShortcutRows(
            ("Double-click", "Open launcher"),
            ("Right-click", "Settings / Quit")));
        shortcuts.Children.Add(Hint("Custom hotkeys are not editable yet — tracked for a later release."));
        _tabs.Items.Add(CreateTab("Shortcuts", WrapScroll(shortcuts)));

        _tabs.SelectionChanged += (_, _) =>
        {
            _saveButton.Visibility = _tabs.SelectedIndex == (int)SettingsTab.Index
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (_tabs.SelectedIndex >= 0 && _tabs.SelectedIndex <= (int)SettingsTab.Shortcuts)
            {
                UpdateStatus(StatusForTab((SettingsTab)_tabs.SelectedIndex));
            }
        };

        root.Children.Add(_tabs);
        Content = root;

        LoadFromOptions(_search.Options);
        LoadGeneralFromStore();
        LoadAppearanceFromStore();
        _tabs.SelectedIndex = (int)initialTab;
        _saveButton.Visibility = initialTab == SettingsTab.Index ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus(StatusForTab(initialTab));
        WinBoxTheme.Changed += OnHostThemeChanged;
        Closed += (_, _) => WinBoxTheme.Changed -= OnHostThemeChanged;
    }

    private string StatusForTab(SettingsTab tab) => tab switch
    {
        SettingsTab.General => _startWithWindowsBox.IsChecked == true
            ? "Will start with Windows sign-in."
            : "Won't start automatically.",
        SettingsTab.Appearance => $"Appearance · {_uiStore.FilePath}",
        SettingsTab.Shortcuts => "Keyboard & tray reference",
        _ => $"Index config: {_indexStore.FilePath}",
    };

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
            Margin = new Thickness(0, 10, 0, 6),
            Tag = "body",
        });
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var value = new TextBlock
        {
            Width = 44,
            TextAlignment = TextAlignment.Right,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "hint",
        };
        DockPanel.SetDock(value, Dock.Right);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = tick,
            IsSnapToTickEnabled = tick >= 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        SettingsChrome.StyleSlider(slider);
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

    private void LoadGeneralFromStore()
    {
        _loadingAppearance = true;
        try
        {
            var options = _uiStore.LoadOrDefault();
            // Prefer persisted preference; fall back to live Run key if JSON never set it.
            _startWithWindowsBox.IsChecked = options.StartWithWindows || _loginAutoStart.IsEnabled();
        }
        finally
        {
            _loadingAppearance = false;
        }
    }

    private void PersistGeneralFromUi()
    {
        if (_loadingAppearance)
        {
            return;
        }

        var enabled = _startWithWindowsBox.IsChecked == true;
        var options = _uiStore.LoadOrDefault();
        options.StartWithWindows = enabled;

        try
        {
            _loginAutoStart.SetEnabled(enabled);
            _uiStore.Save(options);
            UpdateStatus(enabled
                ? "Start with Windows enabled."
                : "Start with Windows disabled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _loadingAppearance = true;
            try
            {
                _startWithWindowsBox.IsChecked = _loginAutoStart.IsEnabled();
            }
            finally
            {
                _loadingAppearance = false;
            }

            UpdateStatus($"Start with Windows failed: {ex.Message}");
        }
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
            WindowEffects.TryEnableSystemChrome(this, WinBoxTheme.IsDarkEffective);
            ApplyThemeChrome();
            UpdateStatus("Appearance saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UpdateStatus($"Appearance save failed: {ex.Message}");
        }
    }

    private void OnHostThemeChanged()
    {
        Dispatcher.Invoke(ApplyThemeChrome);
    }

    private void ApplyThemeChrome()
    {
        Background = WinBoxTheme.SurfaceRaisedBrush;
        Foreground = WinBoxTheme.TextPrimaryBrush;
        _statusText.Foreground = WinBoxTheme.TextSecondaryBrush;
        _recursiveBox.Foreground = WinBoxTheme.TextPrimaryBrush;
        _startWithWindowsBox.Foreground = WinBoxTheme.TextPrimaryBrush;

        SettingsChrome.ApplyTabControl(_tabs);
        SettingsChrome.StyleEmbeddedField(_includeExtensionsBox);
        SettingsChrome.StyleEmbeddedField(_excludeExtensionsBox);
        SettingsChrome.StyleEmbeddedField(_excludePatternsBox);
        SettingsChrome.StyleEmbeddedList(_rootsList);
        SettingsChrome.StyleEmbeddedList(_excludeRootsList);
        SettingsChrome.StyleCombo(_themeBox);
        SettingsChrome.StyleCombo(_scrollModeBox);
        TintComboItems(_themeBox);
        TintComboItems(_scrollModeBox);
        SettingsChrome.StyleSlider(_widthSlider);
        SettingsChrome.StyleSlider(_resultsHeightSlider);
        SettingsChrome.StyleSlider(_fontInputSlider);
        SettingsChrome.StyleSlider(_fontTitleSlider);
        SettingsChrome.StyleSlider(_scrollWidthSlider);
        SettingsChrome.RetintCards(this);
        _footerBar.Background = Brushes.Transparent;
        _footerBar.BorderBrush = WinBoxTheme.BorderSubtleBrush;
        _footerBar.Effect = null;

        if (_saveButton.Parent is Panel buttonRow)
        {
            foreach (var child in buttonRow.Children.OfType<Button>())
            {
                RestyleButton(child, primary: ReferenceEquals(child, _saveButton));
            }
        }

        SettingsChrome.RetintText(this);
        RefreshScrollBars(this);
    }

    private static void TintComboItems(ComboBox box)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            item.Foreground = WinBoxTheme.TextPrimaryBrush;
            item.Background = Brushes.Transparent;
            item.FontFamily = WinBoxTheme.UiFont;
        }
    }

    private static void RefreshScrollBars(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scroll)
            {
                ThemedScrollBars.Apply(scroll);
            }

            RefreshScrollBars(child);
        }
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
        SettingsChrome.FitPathList(list);
    }

    private static void RemoveSelected(ListBox list)
    {
        if (list.SelectedItem is string selected)
        {
            list.Items.Remove(selected);
            SettingsChrome.FitPathList(list);
        }
    }

    private static void FillList(ListBox list, IEnumerable<string> values)
    {
        list.Items.Clear();
        foreach (var value in values)
        {
            list.Items.Add(value);
        }

        SettingsChrome.FitPathList(list);
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
        FocusVisualStyle = null,
    };

    private static ScrollViewer WrapScroll(UIElement content)
    {
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0),
            FocusVisualStyle = null,
        };
        ThemedScrollBars.Apply(scroll);
        return scroll;
    }

    private static UIElement ShortcutRows(params (string Keys, string Description)[] rows)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
        for (var i = 0; i < rows.Length; i++)
        {
            var (keys, description) = rows[i];
            stack.Children.Add(ShortcutRow(keys, description, last: i == rows.Length - 1));
        }

        return stack;
    }

    private static UIElement ShortcutRow(string keys, string description, bool last = false)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, last ? 4 : 8) };
        var key = new TextBlock
        {
            Text = keys,
            Width = 128,
            FontWeight = FontWeights.SemiBold,
            Foreground = WinBoxTheme.AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "shortcut-key",
        };
        DockPanel.SetDock(key, Dock.Left);
        row.Children.Add(key);
        row.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Tag = "body",
        });
        return row;
    }

    private static TextBlock SectionLabel(string text, bool first = false) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = WinBoxTheme.FontTitle,
        Margin = new Thickness(0, first ? 0 : WinBoxTheme.SettingsSectionGap, 0, 4),
        Foreground = WinBoxTheme.TextPrimaryBrush,
        Tag = "section",
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = WinBoxTheme.FontSubtitle,
        Foreground = WinBoxTheme.TextSecondaryBrush,
        Margin = new Thickness(0, 0, 0, 8),
        TextWrapping = TextWrapping.Wrap,
        Tag = "hint",
    };

    private static StackPanel PathListButtons(Action add, Action remove)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
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
            FocusVisualStyle = null,
        };
        RestyleButton(button, primary);
        return button;
    }

    private static void RestyleButton(Button button, bool primary)
    {
        if (primary)
        {
            button.Background = WinBoxTheme.PrimaryButtonBrush;
            button.Foreground = WinBoxTheme.TextOnAccentBrush;
            button.BorderBrush = WinBoxTheme.PrimaryButtonBrush;
        }
        else
        {
            button.Background = WinBoxTheme.SurfaceSunkenBrush;
            button.Foreground = WinBoxTheme.TextPrimaryBrush;
            button.BorderBrush = WinBoxTheme.BorderSubtleBrush;
        }

        button.Template = CreateRoundedButtonTemplate();
    }

    private static ControlTemplate CreateRoundedButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(WinBoxTheme.ControlRadius));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(Button.Background))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });
        border.SetBinding(
            Border.BorderBrushProperty,
            new System.Windows.Data.Binding(nameof(Button.BorderBrush))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });
        border.SetBinding(
            Border.BorderThicknessProperty,
            new System.Windows.Data.Binding(nameof(Button.BorderThickness))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Button.OpacityProperty, 0.92));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Button.OpacityProperty, 0.84));
        template.Triggers.Add(pressed);
        return template;
    }
}
