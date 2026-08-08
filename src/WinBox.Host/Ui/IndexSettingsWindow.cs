using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinBox.Search;
using WinBox.Search.Index;

namespace WinBox.Host.Ui;

/// <summary>
/// Index scope settings: roots, exclude roots, extensions, path denylist.
/// </summary>
internal sealed class IndexSettingsWindow : Window
{
    private readonly SearchPlugin _search;
    private readonly IndexOptionsStore _store;
    private readonly ListBox _rootsList;
    private readonly ListBox _excludeRootsList;
    private readonly TextBox _includeExtensionsBox;
    private readonly TextBox _excludeExtensionsBox;
    private readonly TextBox _excludePatternsBox;
    private readonly CheckBox _recursiveBox;
    private readonly TextBlock _statusText;

    public IndexSettingsWindow(SearchPlugin search, IndexOptionsStore store)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _store = store ?? throw new ArgumentNullException(nameof(store));

        Title = "WinBox — Index settings";
        Width = 600;
        Height = 640;
        MinWidth = 480;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
        Foreground = Brushes.White;

        var root = new DockPanel { Margin = new Thickness(16) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var saveButton = new Button
        {
            Content = "Save & rebuild",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 120,
        };
        saveButton.Click += async (_, _) => await SaveAndRebuildAsync().ConfigureAwait(true);

        var closeButton = new Button
        {
            Content = "Close",
            Padding = new Thickness(12, 6, 12, 6),
            MinWidth = 80,
            IsCancel = true,
        };
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(saveButton);
        buttons.Children.Add(closeButton);
        root.Children.Add(buttons);

        _statusText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(_statusText, Dock.Bottom);
        root.Children.Add(_statusText);

        var form = new StackPanel();

        form.Children.Add(SectionLabel("Index roots"));
        form.Children.Add(Hint("Folders to scan. Start broad; tighten with excludes below."));
        _rootsList = PathListBox();
        form.Children.Add(_rootsList);
        form.Children.Add(PathListButtons(
            add: () => AddFolderToList(_rootsList, "Choose a folder to index"),
            remove: () => RemoveSelected(_rootsList)));

        form.Children.Add(SectionLabel("Exclude roots"));
        form.Children.Add(Hint("Skip these folders (and everything under them), even if inside an index root."));
        _excludeRootsList = PathListBox(height: 88);
        form.Children.Add(_excludeRootsList);
        form.Children.Add(PathListButtons(
            add: () => AddFolderToList(_excludeRootsList, "Choose a folder to exclude"),
            remove: () => RemoveSelected(_excludeRootsList)));

        form.Children.Add(SectionLabel("Include extensions"));
        form.Children.Add(Hint("Empty = all types. Comma-separated, e.g. md, go, txt"));
        _includeExtensionsBox = FieldBox();
        form.Children.Add(_includeExtensionsBox);

        form.Children.Add(SectionLabel("Exclude extensions"));
        form.Children.Add(Hint("Always skipped. Wins over include list. e.g. exe, dll, obj"));
        _excludeExtensionsBox = FieldBox();
        form.Children.Add(_excludeExtensionsBox);

        form.Children.Add(SectionLabel("Exclude path patterns"));
        form.Children.Add(Hint("Skip when any path segment equals the name (one per line). e.g. node_modules, .git"));
        _excludePatternsBox = FieldBox(height: 96, acceptReturn: true);
        form.Children.Add(_excludePatternsBox);

        _recursiveBox = new CheckBox
        {
            Content = "Scan subfolders recursively",
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Brushes.White,
            IsChecked = true,
        };
        form.Children.Add(_recursiveBox);

        var scroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        root.Children.Add(scroll);
        Content = root;

        LoadFromOptions(_search.Options);
        UpdateStatus($"Config file: {_store.FilePath}");
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
            // Not edited in UI yet; preserve any hand-tuned JSON values.
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
            _store.Save(options);
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

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 2),
        Foreground = Brushes.White,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
        Margin = new Thickness(0, 0, 0, 6),
        TextWrapping = TextWrapping.Wrap,
    };

    private static ListBox PathListBox(double height = 100) => new()
    {
        Height = height,
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
    };

    private static StackPanel PathListButtons(Action add, Action remove)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 4),
        };
        var addButton = new Button
        {
            Content = "Add folder…",
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0),
        };
        addButton.Click += (_, _) => add();
        var removeButton = new Button
        {
            Content = "Remove",
            Padding = new Thickness(10, 4, 10, 4),
        };
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
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            CaretBrush = Brushes.White,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 4),
        };

        if (height is not null)
        {
            box.Height = height.Value;
        }

        return box;
    }
}
