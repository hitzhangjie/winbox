using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinBox.Host.Query;
using WinBox.Host.Ui;
using WinBox.Search;
using WinBox.Toolbox;

namespace WinBox.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var registry = new PluginRegistry();
        registry.Register(new SearchPlugin());
        registry.Register(new CalculatorPlugin());
        registry.Register(new ShellPlugin());
        registry.Register(new WebSearchPlugin());
        registry.Register(new AiPlugin());

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        app.Startup += (_, _) => OnStartup(app, registry);

        app.Run();
    }

    private static async void OnStartup(Application app, PluginRegistry registry)
    {
        try
        {
            await registry.StartAllAsync().ConfigureAwait(true);

            var search = registry.GetRequired<Abstractions.ISearchService>();
            await search.IndexPathsAsync(
            [
                @"C:\Users\demo\Documents\report.docx",
                @"C:\Users\demo\Downloads\winbox-notes.md",
                @"D:\Github\winbox\README.md",
            ]).ConfigureAwait(true);

            var router = new QueryRouter(registry.GetMany<Abstractions.IQueryHandler>());
            var overlayState = new LauncherOverlayState();
            var session = new LauncherQuerySession(router, overlayState);
            var overlay = new LauncherOverlayWindow(overlayState, session);
            _ = new WindowInteropHelper(overlay).EnsureHandle();

            GlobalHotkey hotkey;
            try
            {
                hotkey = new GlobalHotkey(overlay, ModifierKeys.Alt | ModifierKeys.Shift, Key.U);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                await registry.StopAllAsync().ConfigureAwait(true);
                app.Shutdown(1);
                return;
            }

            hotkey.Pressed += () => overlay.Dispatcher.Invoke(overlay.ActivateOverlay);

            app.Exit += (_, _) =>
            {
                hotkey.Dispose();
                registry.StopAllAsync().GetAwaiter().GetResult();
            };

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                app.Dispatcher.Invoke(app.Shutdown);
            };

            Console.WriteLine("WinBox host started.");
            Console.WriteLine("  Shift+Alt+U  open launcher");
            Console.WriteLine("  Esc          dismiss");
            Console.WriteLine("  routes: file search | google/gg | math | > cmd | ? ai");
            Console.WriteLine("  Ctrl+C       quit");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup failed: {ex}");
            app.Shutdown(1);
        }
    }
}
