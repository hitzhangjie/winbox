using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Settings-window chrome builders: cards, tabs, rounded fields, compact lists.
/// </summary>
internal static class SettingsChrome
{
    public const string CardTag = "settings-card";
    public const string FieldCardTag = "settings-field-card";
    public const double PathRowExtent = 34;

    public static void ApplyTabControl(TabControl tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        tabs.Background = Brushes.Transparent;
        tabs.BorderThickness = new Thickness(0);
        tabs.Padding = new Thickness(0);
        tabs.FocusVisualStyle = null;
        tabs.Template = CreateTabControlTemplate();
        tabs.ItemContainerStyle = CreateTabItemStyle();
    }

    public static Border WrapCard(UIElement child, bool elevated = true)
    {
        ArgumentNullException.ThrowIfNull(child);
        var card = new Border
        {
            Tag = elevated ? CardTag : FieldCardTag,
            Background = WinBoxTheme.SurfaceOverlayBrush,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(WinBoxTheme.SettingsCardRadius),
            Padding = new Thickness(elevated ? 8 : 4),
            SnapsToDevicePixels = true,
            Effect = elevated ? WindowEffects.CreateCardShadow() : null,
        };
        card.Child = child;
        return card;
    }

    /// <summary>Flat hairline surface — preferred for lists/fields (no floating card look).</summary>
    public static Border WrapFlat(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var frame = new Border
        {
            Tag = FieldCardTag,
            Background = Brushes.Transparent,
            BorderBrush = WinBoxTheme.BorderSubtleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            SnapsToDevicePixels = true,
            Effect = null,
        };
        frame.Child = child;
        return frame;
    }

    public static void TintCard(Border card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var isElevated = Equals(card.Tag, CardTag);
        card.Background = isElevated ? WinBoxTheme.SurfaceOverlayBrush : Brushes.Transparent;
        card.BorderBrush = WinBoxTheme.BorderSubtleBrush;
        card.CornerRadius = new CornerRadius(isElevated ? WinBoxTheme.SettingsCardRadius : 6);
        card.Effect = isElevated ? WindowEffects.CreateCardShadow() : null;
    }

    public static void RetintCards(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Border border
                && (Equals(border.Tag, CardTag) || Equals(border.Tag, FieldCardTag)))
            {
                TintCard(border);
            }

            RetintCards(child);
        }
    }

    public static ListBox CreatePathList(int emptyRows = 1)
    {
        var list = new ListBox
        {
            Background = Brushes.Transparent,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            FontFamily = WinBoxTheme.UiFont,
            FocusVisualStyle = null,
            Tag = emptyRows,
        };
        // Lists expand to show every path; the settings page ScrollViewer handles overflow.
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetCanContentScroll(list, false);

        var template = new DataTemplate(typeof(string));
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        textFactory.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding());
        textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        textFactory.SetValue(TextBlock.PaddingProperty, new Thickness(10, 6, 10, 6));
        textFactory.SetValue(TextBlock.FontFamilyProperty, WinBoxTheme.UiFont);
        template.VisualTree = textFactory;
        list.ItemTemplate = template;
        list.ItemContainerStyle = CreatePathItemStyle();
        FitPathList(list);
        return list;
    }

    /// <summary>Size path lists to all items — no inner vertical scrollbar.</summary>
    public static void FitPathList(ListBox list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var emptyRows = list.Tag is int tagged && tagged > 0 ? tagged : 1;
        var count = list.Items.Count;
        var rows = count <= 0 ? emptyRows : count;
        list.Height = (rows * PathRowExtent) + 4;
        list.MinHeight = emptyRows * PathRowExtent;
        list.MaxHeight = double.PositiveInfinity;
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
    }

    public static Style CreatePathItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(2)));
        style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, CreatePathItemTemplate()));

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

    public static TextBox CreateField(double? height = null, bool acceptReturn = false)
    {
        var box = new TextBox
        {
            AcceptsReturn = acceptReturn,
            TextWrapping = acceptReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
            Foreground = WinBoxTheme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            CaretBrush = WinBoxTheme.AccentBrush,
            Padding = new Thickness(10, 8, 10, 8),
            FontFamily = WinBoxTheme.UiFont,
            FocusVisualStyle = null,
        };

        if (acceptReturn)
        {
            // Grow with content; page ScrollViewer handles overflow.
            box.MinHeight = height ?? (WinBoxTheme.FontSubtitle * 1.35 * 3 + 20);
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            box.TextChanged += (_, _) => FitMultilineField(box);
            box.SizeChanged += (_, _) => FitMultilineField(box);
            FitMultilineField(box);
        }
        else if (height is not null)
        {
            box.Height = height.Value;
        }

        return box;
    }

    /// <summary>Size a multiline field to all lines — no inner vertical scrollbar.</summary>
    public static void FitMultilineField(TextBox box, int minLines = 3)
    {
        ArgumentNullException.ThrowIfNull(box);
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        box.MaxHeight = double.PositiveInfinity;

        var fontSize = box.FontSize > 0 ? box.FontSize : WinBoxTheme.FontSubtitle;
        var lineHeight = Math.Max(16, fontSize * 1.35);
        var padding = box.Padding.Top + box.Padding.Bottom + 4;
        box.MinHeight = (minLines * lineHeight) + padding;

        var lines = CountLogicalLines(box.Text);
        if (box.IsLoaded && box.ActualWidth > 0)
        {
            try
            {
                // Includes soft wraps once layout has run.
                lines = Math.Max(lines, box.LineCount);
            }
            catch (InvalidOperationException)
            {
                // TextBox not ready for LineCount yet.
            }
        }

        lines = Math.Max(minLines, lines);
        var nextHeight = (lines * lineHeight) + padding;
        if (Math.Abs(box.Height - nextHeight) > 0.5)
        {
            box.Height = nextHeight;
        }
    }

    private static int CountLogicalLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    public static void RetintText(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        RetintTextNode(root);
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dependency)
            {
                RetintText(dependency);
            }
        }
    }

    private static void RetintTextNode(DependencyObject node)
    {
        switch (node)
        {
            case TextBlock block:
                block.Foreground = ResolveTextBrush(block.Tag as string);
                break;
            case TextBox box:
                box.Foreground = WinBoxTheme.TextPrimaryBrush;
                box.CaretBrush = WinBoxTheme.AccentBrush;
                break;
            case CheckBox check:
                check.Foreground = WinBoxTheme.TextPrimaryBrush;
                break;
            case ComboBoxItem comboItem:
                comboItem.Foreground = WinBoxTheme.TextPrimaryBrush;
                break;
            case TabItem tab:
                // Force header text off any stale local brush from the previous theme.
                tab.ClearValue(Control.ForegroundProperty);
                break;
        }
    }

    private static SolidColorBrush ResolveTextBrush(string? tag) => tag switch
    {
        "hint" => WinBoxTheme.TextSecondaryBrush,
        "shortcut-key" => WinBoxTheme.AccentBrush,
        _ => WinBoxTheme.TextPrimaryBrush,
    };

    public static void StyleEmbeddedField(TextBox box)
    {
        box.Background = Brushes.Transparent;
        box.Foreground = WinBoxTheme.TextPrimaryBrush;
        box.BorderThickness = new Thickness(0);
        box.CaretBrush = WinBoxTheme.AccentBrush;
        box.FocusVisualStyle = null;
        if (box.AcceptsReturn)
        {
            FitMultilineField(box);
        }
    }

    public static void StyleEmbeddedList(ListBox list)
    {
        list.Background = Brushes.Transparent;
        list.Foreground = WinBoxTheme.TextPrimaryBrush;
        list.BorderThickness = new Thickness(0);
        list.FocusVisualStyle = null;
        list.ItemContainerStyle = CreatePathItemStyle();
        FitPathList(list);
    }

    public static void StyleCombo(ComboBox box)
    {
        box.Background = WinBoxTheme.SurfaceSunkenBrush;
        box.Foreground = WinBoxTheme.TextPrimaryBrush;
        box.BorderBrush = WinBoxTheme.BorderSubtleBrush;
        box.Padding = new Thickness(10, 8, 10, 8);
        box.BorderThickness = new Thickness(1);
        box.FocusVisualStyle = null;
        box.Template = CreateComboTemplate();
    }

    public static void StyleSlider(Slider slider)
    {
        ArgumentNullException.ThrowIfNull(slider);
        slider.FocusVisualStyle = null;
        slider.Template = CreateSliderTemplate();
    }

    private static ControlTemplate CreateComboTemplate()
    {
        var surface = Hex(WinBoxTheme.SurfaceSunkenBrush.Color);
        var border = Hex(WinBoxTheme.BorderSubtleBrush.Color);
        var text = Hex(WinBoxTheme.TextPrimaryBrush.Color);
        var hover = Hex(WinBoxTheme.HoverBrush.Color);
        var radius = WinBoxTheme.ControlRadius.ToString("0.##", CultureInfo.InvariantCulture);
        var xaml = """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="ComboBox">
              <Grid>
                <ToggleButton x:Name="ToggleButton"
                              Focusable="False"
                              FocusVisualStyle="{x:Null}"
                              IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                              ClickMode="Press">
                  <ToggleButton.Template>
                    <ControlTemplate TargetType="ToggleButton">
                      <Border x:Name="Chrome"
                              Background="__SURFACE__"
                              BorderBrush="__BORDER__"
                              BorderThickness="1"
                              CornerRadius="__RADIUS__"
                              SnapsToDevicePixels="True">
                        <Grid>
                          <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="28"/>
                          </Grid.ColumnDefinitions>
                          <Path Grid.Column="1"
                                HorizontalAlignment="Center" VerticalAlignment="Center"
                                Data="M 0 0 L 6 6 L 12 0"
                                Stroke="__TEXT__" StrokeThickness="1.5"
                                StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
                        </Grid>
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="Chrome" Property="Background" Value="__HOVER__"/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter Margin="12,8,32,8"
                                  IsHitTestVisible="False"
                                  VerticalAlignment="Center"
                                  HorizontalAlignment="Left"
                                  TextElement.Foreground="{TemplateBinding Foreground}"
                                  Content="{TemplateBinding SelectionBoxItem}"
                                  ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                  RecognizesAccessKey="True"/>
                <Popup x:Name="PART_Popup"
                       Placement="Bottom"
                       IsOpen="{TemplateBinding IsDropDownOpen}"
                       AllowsTransparency="True"
                       Focusable="False"
                       PopupAnimation="Fade">
                  <Border Margin="0,4,0,0"
                          Background="__SURFACE__"
                          BorderBrush="__BORDER__"
                          BorderThickness="1"
                          CornerRadius="__RADIUS__"
                          MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
                          MaxHeight="220"
                          SnapsToDevicePixels="True">
                    <ScrollViewer Margin="2" CanContentScroll="True" FocusVisualStyle="{x:Null}">
                      <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Contained"/>
                    </ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
            </ControlTemplate>
            """
            .Replace("__SURFACE__", surface, StringComparison.Ordinal)
            .Replace("__BORDER__", border, StringComparison.Ordinal)
            .Replace("__TEXT__", text, StringComparison.Ordinal)
            .Replace("__HOVER__", hover, StringComparison.Ordinal)
            .Replace("__RADIUS__", radius, StringComparison.Ordinal);
        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static ControlTemplate CreateSliderTemplate()
    {
        var track = Hex(WinBoxTheme.BorderSubtleBrush.Color);
        var fill = Hex(WinBoxTheme.AccentBrush.Color);
        var thumbFill = Hex(WinBoxTheme.SurfaceOverlayBrush.Color);
        var xaml = """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="Slider">
              <Grid VerticalAlignment="Center" Height="24" Background="Transparent">
                <Border x:Name="TrackBg" Height="3" CornerRadius="1.5" Background="__TRACK__" VerticalAlignment="Center" Margin="8,0"/>
                <Track x:Name="PART_Track">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False" IsTabStop="False">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton">
                          <Border Height="3" CornerRadius="1.5" Background="__FILL__" Margin="8,0,0,0" VerticalAlignment="Center"/>
                        </ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Width="18" Height="18" FocusVisualStyle="{x:Null}">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Grid>
                            <Ellipse Width="18" Height="18" Fill="__FILL__" Opacity="0.22"/>
                            <Ellipse Width="14" Height="14" Fill="__THUMBFILL__" Stroke="__FILL__" StrokeThickness="2"
                                     HorizontalAlignment="Center" VerticalAlignment="Center"/>
                          </Grid>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Command="{x:Static Slider.IncreaseLarge}" Opacity="0" Focusable="False" IsTabStop="False"/>
                  </Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
            """
            .Replace("__TRACK__", track, StringComparison.Ordinal)
            .Replace("__FILL__", fill, StringComparison.Ordinal)
            .Replace("__THUMBFILL__", thumbFill, StringComparison.Ordinal);
        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static ControlTemplate CreatePathItemTemplate()
    {
        var template = new ControlTemplate(typeof(ListBoxItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(ListBoxItem.Background))
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

    private static ControlTemplate CreateTabControlTemplate()
    {
        var raised = Hex(WinBoxTheme.SurfaceRaisedBrush.Color);
        var border = Hex(WinBoxTheme.BorderSubtleBrush.Color);
        var pad = WinBoxTheme.SettingsContentPadding.ToString("0.##", CultureInfo.InvariantCulture);
        var padRight = WinBoxTheme.SettingsContentPaddingRight.ToString("0.##", CultureInfo.InvariantCulture);
        var radius = WinBoxTheme.SettingsCardRadius.ToString("0.##", CultureInfo.InvariantCulture);
        var xaml = $"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="TabControl">
              <Grid>
                <Grid.RowDefinitions>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <Border Grid.Row="0"
                        BorderBrush="{border}"
                        BorderThickness="0,0,0,1"
                        Padding="4,0,4,0"
                        Background="Transparent">
                  <TabPanel IsItemsHost="True" Background="Transparent"/>
                </Border>
                <Border Grid.Row="1"
                        Background="{raised}"
                        Padding="{pad},{pad},{padRight},{pad}"
                        CornerRadius="0,0,{radius},{radius}">
                  <ContentPresenter x:Name="PART_SelectedContentHost"
                                    ContentSource="SelectedContent"/>
                </Border>
              </Grid>
            </ControlTemplate>
            """;
        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static Style CreateTabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, WinBoxTheme.TextSecondaryBrush));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, WinBoxTheme.UiFont));
        style.Setters.Add(new Setter(Control.FontSizeProperty, WinBoxTheme.FontTitle));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 10, 14, 10)));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateTabItemTemplate()));

        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.ForegroundProperty, WinBoxTheme.TextPrimaryBrush));
        selected.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(selected);
        return style;
    }

    private static ControlTemplate CreateTabItemTemplate()
    {
        var accent = Hex(WinBoxTheme.AccentBrush.Color);
        var hover = Hex(WinBoxTheme.HoverBrush.Color);
        var foregroundBind = "{TemplateBinding Foreground}";
        var xaml = $"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="TabItem">
              <Grid Background="Transparent" SnapsToDevicePixels="True">
                <Border x:Name="Bd"
                        Background="Transparent"
                        CornerRadius="8,8,0,0"
                        Padding="14,10,14,10"
                        Margin="2,0,2,0">
                  <ContentPresenter ContentSource="Header"
                                    HorizontalAlignment="Center"
                                    VerticalAlignment="Center"
                                    TextElement.Foreground="{foregroundBind}"
                                    RecognizesAccessKey="True"/>
                </Border>
                <Border x:Name="Underline"
                        Height="2"
                        VerticalAlignment="Bottom"
                        Margin="10,0,10,0"
                        CornerRadius="1"
                        Background="Transparent"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="Bd" Property="Background" Value="{hover}"/>
                </Trigger>
                <Trigger Property="IsSelected" Value="True">
                  <Setter TargetName="Underline" Property="Background" Value="{accent}"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """;
        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
