using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WinBox.Abstractions;

namespace WinBox.Host.Ui;

/// <summary>List item model for launcher results (Host chrome; plugins supply the strings).</summary>
public sealed class ResultRowModel
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public string? ToolTipText { get; init; }

    public ResultActionKind Action { get; init; }

    public string Glyph => WinBoxTheme.GlyphForAction(Action);

    public static ResultRowModel FromResult(QueryResultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var subtitle = string.IsNullOrWhiteSpace(item.Subtitle) ? null : item.Subtitle;
        var tip = subtitle ?? item.Title;
        return new ResultRowModel
        {
            Title = item.Title,
            Subtitle = subtitle,
            ToolTipText = tip,
            Action = item.Action,
        };
    }
}

/// <summary>Two-line result row: glyph + title + truncated subtitle.</summary>
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

    private readonly TextBlock _glyph;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;

    public ResultRowView()
    {
        MinHeight = WinBoxTheme.ResultRowMinHeight;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _glyph = new TextBlock
        {
            FontFamily = WinBoxTheme.GlyphFont,
            FontSize = WinBoxTheme.FontGlyph,
            Foreground = WinBoxTheme.TextSecondaryBrush,
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
            Foreground = WinBoxTheme.TextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        _subtitle = new TextBlock
        {
            FontFamily = WinBoxTheme.UiFont,
            FontSize = UiLayout.FontSubtitle,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 1, 0, 0),
        };
        textStack.Children.Add(_title);
        textStack.Children.Add(_subtitle);
        SetColumn(textStack, 1);
        Children.Add(textStack);
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

    public static DataTemplate CreateListTemplate()
    {
        var template = new DataTemplate(typeof(ResultRowModel));
        var factory = new FrameworkElementFactory(typeof(ResultRowView));
        factory.SetBinding(TitleProperty, new Binding(nameof(ResultRowModel.Title)));
        factory.SetBinding(SubtitleProperty, new Binding(nameof(ResultRowModel.Subtitle)));
        factory.SetBinding(GlyphProperty, new Binding(nameof(ResultRowModel.Glyph)));
        factory.SetBinding(ToolTipProperty, new Binding(nameof(ResultRowModel.ToolTipText)));
        template.VisualTree = factory;
        return template;
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
}
