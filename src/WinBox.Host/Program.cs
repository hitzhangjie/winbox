using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinBox.Host.Query;
using WinBox.Host.Ui;
using WinBox.Search;
using WinBox.Search.Index;
using WinBox.Toolbox;

namespace WinBox.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // WinForms NotifyIcon needs visual styles when hosted from WPF.
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        var optionsStore = new IndexOptionsStore(IndexOptionsStore.DefaultFilePath);
        var indexOptions = optionsStore.LoadOrDefault();
        var uiStore = new UiOptionsStore(UiOptionsStore.DefaultFilePath);
        var uiOptions = uiStore.LoadOrDefault();
        WinBoxTheme.Apply(WinBoxTheme.ParseTheme(uiOptions.Theme));
        UiLayout.Apply(uiOptions);

        var registry = new PluginRegistry();
        var searchPlugin = new SearchPlugin(indexOptions);
        registry.Register(searchPlugin);
        registry.Register(new CalculatorPlugin());
        registry.Register(new ShellPlugin());
        registry.Register(new WebSearchPlugin());
        registry.Register(new AiPlugin());

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        app.Startup += (_, _) => OnStartup(app, registry, searchPlugin, optionsStore, uiStore);

        app.Run();
    }

    private static async void OnStartup(
        Application app,
        PluginRegistry registry,
        SearchPlugin searchPlugin,
        IndexOptionsStore optionsStore,
        UiOptionsStore uiStore)
    {
        AppTrayIcon? tray = null;
        GlobalHotkey? launcherHotkey = null;

        try
        {
            await registry.StartAllAsync().ConfigureAwait(true);

            await searchPlugin.RebuildIndexAsync().ConfigureAwait(true);
            Console.WriteLine($"Indexed {searchPlugin.IndexedCount} file(s) from configured roots.");

            var router = new QueryRouter(registry.GetMany<Abstractions.IQueryHandler>());
            var overlayState = new LauncherOverlayState();
            var session = new LauncherQuerySession(router, overlayState);
            var overlay = new LauncherOverlayWindow(overlayState, session, uiStore);
            _ = new WindowInteropHelper(overlay).EnsureHandle();

            IndexSettingsWindow? settingsWindow = null;

            void OpenSettings(SettingsTab tab)
            {
                if (settingsWindow is { IsLoaded: true })
                {
                    settingsWindow.ShowTab(tab);
                    BringSettingsToFront(settingsWindow);
                    return;
                }

                settingsWindow = new IndexSettingsWindow(searchPlugin, optionsStore, uiStore, tab);
                settingsWindow.Closed += (_, _) => settingsWindow = null;
                settingsWindow.Show();
                BringSettingsToFront(settingsWindow);
            }

            static void BringSettingsToFront(Window window)
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Show();
                window.Activate();
                window.Topmost = true;
                window.Topmost = false;
                _ = window.Focus();
            }

            try
            {
                launcherHotkey = new GlobalHotkey(overlay, ModifierKeys.Alt | ModifierKeys.Shift, Key.U);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Launcher hotkey unavailable; use the tray icon instead.");
            }

            if (launcherHotkey is not null)
            {
                launcherHotkey.Pressed += () => overlay.Dispatcher.Invoke(overlay.ActivateOverlay);
            }

            tray = new AppTrayIcon(overlay.Dispatcher);
            tray.OpenLauncherRequested += () => overlay.ActivateOverlay();
            tray.OpenSettingsRequested += () => OpenSettings(SettingsTab.Index);
            tray.ExitRequested += () => app.Shutdown();
            tray.ShowBalloon(
                "WinBox",
                $"Ready — {searchPlugin.IndexedCount} files indexed. Right-click tray for Settings.");

            app.Exit += (_, _) =>
            {
                tray?.Dispose();
                launcherHotkey?.Dispose();
                registry.StopAllAsync().GetAwaiter().GetResult();
            };

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                app.Dispatcher.Invoke(app.Shutdown);
            };

            Console.WriteLine("WinBox host started.");
            Console.WriteLine("  Tray icon     right-click → Settings / Quit");
            Console.WriteLine("  Tray double-click → open launcher");
            if (launcherHotkey is not null)
            {
                Console.WriteLine("  Shift+Alt+U  open launcher");
            }

            Console.WriteLine("  Esc          dismiss launcher");
            Console.WriteLine("  routes: file search | google/gg | math | > cmd | ? ai");
            Console.WriteLine("  Ctrl+C       quit");
        }
        catch (Exception ex)
        {
            tray?.Dispose();
            launcherHotkey?.Dispose();
            Console.Error.WriteLine($"Startup failed: {ex}");
            app.Shutdown(1);
        }
    }
}
