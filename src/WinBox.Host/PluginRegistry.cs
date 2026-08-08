using WinBox.Abstractions;

namespace WinBox.Host;

/// <summary>
/// Minimal in-process plugin registry. Later: discovery from disk + isolation.
/// </summary>
public sealed class PluginRegistry
{
    private readonly Dictionary<string, IWinBoxPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IWinBoxPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (!_plugins.TryAdd(plugin.Id, plugin))
        {
            throw new InvalidOperationException($"Plugin '{plugin.Id}' is already registered.");
        }
    }

    public IReadOnlyCollection<IWinBoxPlugin> Plugins => _plugins.Values;

    public T GetRequired<T>() where T : class
    {
        var match = _plugins.Values.OfType<T>().FirstOrDefault();
        if (match is null)
        {
            throw new InvalidOperationException($"No plugin implements {typeof(T).Name}.");
        }

        return match;
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plugin in _plugins.Values)
        {
            await plugin.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plugin in _plugins.Values.Reverse())
        {
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
