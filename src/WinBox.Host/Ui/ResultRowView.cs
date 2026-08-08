using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WinBox.Abstractions;
using IOPath = System.IO.Path;

namespace WinBox.Host.Ui;

/// <summary>List item model for launcher results (Host chrome; plugins supply the strings).</summary>
public sealed class ResultRowModel
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public string? ToolTipText { get; init; }

    public ResultActionKind Action { get; init; }

    public string? IconKey { get; init; }

    /// <summary>Explorer-style shell icon when the payload is a file path; null uses <see cref="Glyph"/>.</summary>
    public ImageSource? IconImage { get; init; }

    /// <summary>Wrap title as a growing multi-line body (AI answers).</summary>
    public bool Multiline { get; init; }

    public string Glyph => WinBoxTheme.GlyphForResult(IconKey, Action);

    public static ResultRowModel FromResult(QueryResultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var subtitle = string.IsNullOrWhiteSpace(item.Subtitle) ? null : item.Subtitle;
        // Multiline AI body is already fully visible in-row — no hover popup.
        var tip = item.Multiline ? null : (subtitle ?? item.Title);
        var path = ResolvePathHint(item);
        return new ResultRowModel
        {
            Title = item.Title,
            Subtitle = subtitle,
            ToolTipText = tip,
            Action = item.Action,
            IconKey = string.IsNullOrWhiteSpace(item.IconKey) ? null : item.IconKey,
            IconImage = path is null ? null : ShellFileIcons.GetForPath(path),
            Multiline = item.Multiline,
        };
    }

    private static string? ResolvePathHint(QueryResultItem item)
    {
        if (item.Action is not (ResultActionKind.OpenPath or ResultActionKind.OpenContainingFolder))
        {
            return null;
        }

        if (IsRootedPath(item.Payload))
        {
            return item.Payload;
        }

        if (IsRootedPath(item.Id))
        {
            return item.Id;
        }

        return null;
    }

    private static bool IsRootedPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return IOPath.IsPathRooted(value);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>Two-line result row: shell icon or glyph + title + truncated subtitle.</summary>
public sealed class ResultRowView : Grid
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(ResultRowView),
        new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(ResultRowView),
        new PropertyMetadata(null, OnSubtitleChanged));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(ResultRowView),
        new PropertyMetadata(WinBoxTheme.GlyphForAction(ResultActionKind.None), OnGlyphChanged));

    public static readonly DependencyProperty IconImageProperty = DependencyProperty.Register(
        nameof(IconImage),
        typeof(ImageSource),
        typeof(ResultRowView),
        new PropertyMetadata(null, OnIconImageChanged));

    public static readonly DependencyProperty MultilineProperty = DependencyProperty.Register(
        nameof(Multiline),
        typeof(bool),
        typeof(ResultRowView),
        new PropertyMetadata(false, OnMultilineChanged));

    private readonly Image _icon;
    private readonly TextBlock _glyph;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;

    public ResultRowView()
    {
        MinHeight = WinBoxTheme.ResultRowMinHeight;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _icon = new Image
        {
            Width = WinBoxTheme.ResultIconSize,
            Height = WinBoxTheme.ResultIconSize,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Visibility = Visibility.Collapsed,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
        SetColumn(_icon, 0);
        Children.Add(_icon);

        _glyph = new TextBlock
        {
            FontFamily = WinBoxTheme.GlyphFont,
            FontSize = WinBoxTheme.FontGlyph,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        SetColumn(_glyph, 0);
        Children.Add(_glyph);

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        _title = new TextBlock
        {
            FontFamily = WinBoxTheme.UiFont,
            FontSize = UiLayout.FontTitle,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        _subtitle = new TextBlock
        {
            FontFamily = WinBoxTheme.UiFont,
            FontSize = UiLayout.FontSubtitle,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 1, 0, 0),
        };
        textStack.Children.Add(_title);
        textStack.Children.Add(_subtitle);
        SetColumn(textStack, 1);
        Children.Add(textStack);
        ApplyThemeBrushes();
        SyncIconPresentation();
        ApplyMultilineLayout();
    }

    public void ApplyThemeBrushes()
    {
        _glyph.Foreground = WinBoxTheme.TextSecondaryBrush;
        _title.Foreground = WinBoxTheme.TextPrimaryBrush;
        _subtitle.Foreground = WinBoxTheme.TextSecondaryBrush;
        _title.FontSize = UiLayout.FontTitle;
        _subtitle.FontSize = UiLayout.FontSubtitle;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public ImageSource? IconImage
    {
        get => (ImageSource?)GetValue(IconImageProperty);
        set => SetValue(IconImageProperty, value);
    }

    public bool Multiline
    {
        get => (bool)GetValue(MultilineProperty);
        set => SetValue(MultilineProperty, value);
    }

    public static DataTemplate CreateListTemplate()
    {
        var template = new DataTemplate(typeof(ResultRowModel));
        var factory = new FrameworkElementFactory(typeof(ResultRowView));
        factory.SetBinding(TitleProperty, new Binding(nameof(ResultRowModel.Title)));
        factory.SetBinding(SubtitleProperty, new Binding(nameof(ResultRowModel.Subtitle)));
        factory.SetBinding(GlyphProperty, new Binding(nameof(ResultRowModel.Glyph)));
        factory.SetBinding(IconImageProperty, new Binding(nameof(ResultRowModel.IconImage)));
        factory.SetBinding(MultilineProperty, new Binding(nameof(ResultRowModel.Multiline)));
        factory.SetBinding(ToolTipProperty, new Binding(nameof(ResultRowModel.ToolTipText)));
        template.VisualTree = factory;
        return template;
    }

    private void SyncIconPresentation()
    {
        if (IconImage is not null)
        {
            _icon.Source = IconImage;
            _icon.Visibility = Visibility.Visible;
            _glyph.Visibility = Visibility.Collapsed;
        }
        else
        {
            _icon.Source = null;
            _icon.Visibility = Visibility.Collapsed;
            _glyph.Visibility = Visibility.Visible;
        }
    }

    private void ApplyMultilineLayout()
    {
        if (Multiline)
        {
            _title.TextWrapping = TextWrapping.Wrap;
            _title.TextTrimming = TextTrimming.None;
            // No MaxHeight here — the results ListBox caps height and scrolls (settings-style bar).
            _title.MaxHeight = double.PositiveInfinity;
            _glyph.VerticalAlignment = VerticalAlignment.Top;
            _icon.VerticalAlignment = VerticalAlignment.Top;
            _glyph.Margin = new Thickness(0, 4, 8, 0);
            _icon.Margin = new Thickness(0, 4, 8, 0);
            Margin = new Thickness(0, 2, 0, 2);
        }
        else
        {
            _title.TextWrapping = TextWrapping.NoWrap;
            _title.TextTrimming = TextTrimming.CharacterEllipsis;
            _title.MaxHeight = double.PositiveInfinity;
            _glyph.VerticalAlignment = VerticalAlignment.Center;
            _icon.VerticalAlignment = VerticalAlignment.Center;
            _glyph.Margin = new Thickness(0, 0, 8, 0);
            _icon.Margin = new Thickness(0, 0, 8, 0);
            Margin = new Thickness(0);
        }
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResultRowView row)
        {
            row._title.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ResultRowView row)
        {
            return;
        }

        var text = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            row._subtitle.Text = string.Empty;
            row._subtitle.Visibility = Visibility.Collapsed;
        }
        else
        {
            row._subtitle.Text = text;
            row._subtitle.Visibility = Visibility.Visible;
        }
    }

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResultRowView row)
        {
            row._glyph.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private static void OnIconImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResultRowView row)
        {
            row.SyncIconPresentation();
        }
    }

    private static void OnMultilineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResultRowView row)
        {
            row.ApplyMultilineLayout();
        }
    }
}
