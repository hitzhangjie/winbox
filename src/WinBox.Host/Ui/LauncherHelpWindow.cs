using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinBox.Host.Ui;

/// <summary>Tray Help — what you can type and which actions fire.</summary>
internal sealed class LauncherHelpWindow : Window
{
    private readonly Button _closeButton;

    public LauncherHelpWindow()
    {
        Title = LauncherHelpText.WindowTitle;
        Width = WinBoxTheme.SettingsWindowWidth;
        Height = WinBoxTheme.SettingsWindowHeight;
        MinWidth = WinBoxTheme.SettingsWindowMinWidth;
        MinHeight = WinBoxTheme.SettingsWindowMinHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
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

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        _closeButton = new Button
        {
            Content = "Close",
            IsCancel = true,
            IsDefault = true,
            Padding = new Thickness(16, 8, 16, 8),
            MinWidth = 96,
            FontFamily = WinBoxTheme.UiFont,
            Cursor = Cursors.Hand,
            Background = WinBoxTheme.PrimaryButtonBrush,
            Foreground = WinBoxTheme.TextOnAccentBrush,
            BorderBrush = WinBoxTheme.PrimaryButtonBrush,
            BorderThickness = new Thickness(0),
            FocusVisualStyle = null,
        };
        _closeButton.Click += (_, _) => Close();
        buttons.Children.Add(_closeButton);
        root.Children.Add(buttons);

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = LauncherHelpText.HelpIntro,
            FontSize = WinBoxTheme.FontSubtitle,
            Foreground = WinBoxTheme.TextSecondaryBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Tag = "hint",
        });

        body.Children.Add(SectionLabel(LauncherHelpText.HelpModesHeading, first: true));
        body.Children.Add(HelpRows(LauncherHelpText.QueryModes, keyWidth: 148));

        body.Children.Add(SectionLabel(LauncherHelpText.HelpKeysHeading));
        body.Children.Add(HelpRows(LauncherHelpText.Shortcuts, keyWidth: 128));

        body.Children.Add(SectionLabel(LauncherHelpText.HelpTrayHeading));
        body.Children.Add(HelpRows(LauncherHelpText.TrayActions, keyWidth: 128));

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FocusVisualStyle = null,
        };
        ThemedScrollBars.Apply(scroll);
        root.Children.Add(scroll);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        WinBoxTheme.Changed += OnThemeChanged;
        Closed += (_, _) => WinBoxTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        void Apply()
        {
            Background = WinBoxTheme.SurfaceRaisedBrush;
            Foreground = WinBoxTheme.TextPrimaryBrush;
            _closeButton.Background = WinBoxTheme.PrimaryButtonBrush;
            _closeButton.Foreground = WinBoxTheme.TextOnAccentBrush;
            _closeButton.BorderBrush = WinBoxTheme.PrimaryButtonBrush;
            WindowEffects.TryEnableSystemChrome(this, WinBoxTheme.IsDarkEffective);
            RetintTree(this);
        }

        if (Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.Invoke(Apply);
        }
    }

    private static void RetintTree(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb)
            {
                tb.Foreground = Equals(tb.Tag, "hint")
                    ? WinBoxTheme.TextSecondaryBrush
                    : Equals(tb.Tag, "shortcut-key")
                        ? WinBoxTheme.AccentBrush
                        : WinBoxTheme.TextPrimaryBrush;
            }

            RetintTree(child);
        }
    }

    private static TextBlock SectionLabel(string text, bool first = false) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = WinBoxTheme.FontTitle,
        Margin = new Thickness(0, first ? 0 : WinBoxTheme.SettingsSectionGap, 0, 6),
        Foreground = WinBoxTheme.TextPrimaryBrush,
        Tag = "section",
    };

    private static UIElement HelpRows(IReadOnlyList<(string Key, string Description)> rows, double keyWidth)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        for (var i = 0; i < rows.Count; i++)
        {
            var (key, description) = rows[i];
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, i == rows.Count - 1 ? 4 : 10) };
            var keyBlock = new TextBlock
            {
                Text = key,
                Width = keyWidth,
                FontWeight = FontWeights.SemiBold,
                Foreground = WinBoxTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                Tag = "shortcut-key",
            };
            DockPanel.SetDock(keyBlock, Dock.Left);
            row.Children.Add(keyBlock);
            row.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = WinBoxTheme.TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = "shortcut-desc",
            });
            stack.Children.Add(row);
        }

        return stack;
    }
}
