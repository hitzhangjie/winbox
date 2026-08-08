using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>
/// Theme-colored thin scrollbars for launcher / settings lists.
/// Default WPF bars are light-chrome and clash with dark surfaces.
/// </summary>
internal static class ThemedScrollBars
{
    public static void Apply(FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var width = Math.Clamp(UiLayout.ScrollBarWidth, 4, 16);
        target.Resources[typeof(ScrollBar)] = CreateScrollBarStyle(width);
    }

    private static Style CreateScrollBarStyle(double thickness)
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateTemplate(vertical: true, thickness)));

        var horizontal = new Trigger
        {
            Property = ScrollBar.OrientationProperty,
            Value = Orientation.Horizontal,
        };
        horizontal.Setters.Add(new Setter(Control.TemplateProperty, CreateTemplate(vertical: false, thickness)));
        style.Triggers.Add(horizontal);
        return style;
    }

    private static ControlTemplate CreateTemplate(bool vertical, double thickness)
    {
        var radius = Math.Max(2, thickness / 2).ToString("0.##", CultureInfo.InvariantCulture);
        var size = thickness.ToString("0.##", CultureInfo.InvariantCulture);
        var track = Hex(WinBoxTheme.SurfaceSunkenBrush.Color);
        var thumb = Hex(WinBoxTheme.TextSecondaryBrush.Color);
        var thumbHover = Hex(WinBoxTheme.AccentBrush.Color);
        var sizeAttr = vertical ? $"Width=\"{size}\"" : $"Height=\"{size}\"";
        var reversed = vertical ? "True" : "False";
        var decCmd = vertical ? "{x:Static ScrollBar.LineUpCommand}" : "{x:Static ScrollBar.LineLeftCommand}";
        var incCmd = vertical ? "{x:Static ScrollBar.LineDownCommand}" : "{x:Static ScrollBar.LineRightCommand}";

        var xaml = $"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="ScrollBar">
              <Grid {sizeAttr} Background="Transparent">
                <Border Background="{track}" Opacity="0.4" CornerRadius="{radius}"/>
                <Track x:Name="PART_Track" IsDirectionReversed="{reversed}" Margin="1">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="{decCmd}" Opacity="0" Width="0" Height="0" IsTabStop="False"/>
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb>
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Border x:Name="ThumbBd" Background="{thumb}" CornerRadius="{radius}" Margin="1" Opacity="0.9"/>
                          <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                              <Setter TargetName="ThumbBd" Property="Background" Value="{thumbHover}"/>
                              <Setter TargetName="ThumbBd" Property="Opacity" Value="1"/>
                            </Trigger>
                            <Trigger Property="IsDragging" Value="True">
                              <Setter TargetName="ThumbBd" Property="Background" Value="{thumbHover}"/>
                            </Trigger>
                          </ControlTemplate.Triggers>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Command="{incCmd}" Opacity="0" Width="0" Height="0" IsTabStop="False"/>
                  </Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
            """;

        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
