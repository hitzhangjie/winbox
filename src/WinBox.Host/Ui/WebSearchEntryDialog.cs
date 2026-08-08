using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinBox.Toolbox;

namespace WinBox.Host.Ui;

/// <summary>Modal Add / Edit dialog for a single web-search entry.</summary>
internal sealed class WebSearchEntryDialog : Window
{
    private readonly TextBox _keywordsBox;
    private readonly TextBox _nameBox;
    private readonly TextBox _urlBox;
    private readonly TextBlock _errorText;
    private readonly bool _enabled;

    public WebSearchEntry? Result { get; private set; }

    public WebSearchEntryDialog(Window owner, WebSearchEntry? existing)
    {
        Owner = owner;
        Title = existing is null ? "Add web search" : "Edit web search";
        Width = 480;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = WinBoxTheme.SurfaceRaisedBrush;
        Foreground = WinBoxTheme.TextPrimaryBrush;
        FontFamily = WinBoxTheme.UiFont;
        FocusVisualStyle = null;
        WindowIconFactory.Apply(this);
        _enabled = existing?.Enabled ?? true;

        SourceInitialized += (_, _) =>
        {
            WindowEffects.TryEnableSystemChrome(this, WinBoxTheme.IsDarkEffective);
        };

        var root = new DockPanel { Margin = new Thickness(WinBoxTheme.SettingsPageMargin) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var saveButton = CreateButton("Save", primary: true, minWidth: 96);
        saveButton.IsDefault = true;
        saveButton.Click += (_, _) => TryAccept();

        var cancelButton = CreateButton("Cancel", primary: false, minWidth: 88);
        cancelButton.IsCancel = true;
        cancelButton.Margin = new Thickness(8, 0, 0, 0);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        buttons.Children.Add(saveButton);
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);

        var form = new StackPanel();
        form.Children.Add(Hint(
            "Keywords trigger the search after a space (e.g. gg winbox). Separate multiple with commas."));

        form.Children.Add(FieldLabel("Keywords"));
        _keywordsBox = SettingsChrome.CreateField();
        form.Children.Add(SettingsChrome.WrapFlat(_keywordsBox));

        form.Children.Add(FieldLabel("Display name"));
        _nameBox = SettingsChrome.CreateField();
        form.Children.Add(SettingsChrome.WrapFlat(_nameBox));

        form.Children.Add(FieldLabel("URL template"));
        form.Children.Add(Hint("Must include {query} for the search text."));
        _urlBox = SettingsChrome.CreateField(height: 72, acceptReturn: true);
        form.Children.Add(SettingsChrome.WrapFlat(_urlBox));

        _errorText = new TextBlock
        {
            Foreground = WinBoxTheme.AccentBrush,
            FontSize = WinBoxTheme.FontSubtitle,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed,
            Tag = "hint",
        };
        form.Children.Add(_errorText);

        root.Children.Add(form);
        Content = root;

        if (existing is not null)
        {
            _keywordsBox.Text = WebSearchOptionsStore.JoinKeywords(existing.Keywords);
            _nameBox.Text = existing.DisplayName;
            _urlBox.Text = existing.UrlTemplate;
        }
        else
        {
            _urlBox.Text = "https://example.com/search?q={query}";
        }

        Loaded += (_, _) =>
        {
            _keywordsBox.Focus();
            _keywordsBox.SelectAll();
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        };
    }

    private void TryAccept()
    {
        var keywords = WebSearchOptionsStore.SplitKeywords(_keywordsBox.Text);
        var url = _urlBox.Text.Trim();
        var name = _nameBox.Text.Trim();

        if (keywords.Count == 0)
        {
            ShowError("Enter at least one keyword (no spaces inside a keyword).");
            _keywordsBox.Focus();
            return;
        }

        if (url.Length == 0)
        {
            ShowError("Enter a URL template.");
            _urlBox.Focus();
            return;
        }

        if (!url.Contains("{query}", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("{0}", StringComparison.Ordinal))
        {
            ShowError("URL must include {query}.");
            _urlBox.Focus();
            return;
        }

        if (name.Length == 0)
        {
            name = keywords[0];
        }

        Result = new WebSearchEntry(keywords, name, url, _enabled);
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = WinBoxTheme.FontSubtitle,
        Foreground = WinBoxTheme.TextPrimaryBrush,
        Margin = new Thickness(0, 10, 0, 4),
        Tag = "body",
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

    private static Button CreateButton(string content, bool primary, double minWidth = 0)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = minWidth,
            FontFamily = WinBoxTheme.UiFont,
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1),
            FocusVisualStyle = null,
        };

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
        return button;
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
