namespace WinBox.Abstractions;

/// <summary>
/// Contract every WinBox capability plugin must implement.
/// </summary>
public interface IWinBoxPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
