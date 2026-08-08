using WinBox.Abstractions;
using WinBox.Host.Query;

namespace WinBox.Host.Ui;

/// <summary>
/// Debounced bridge from overlay text → <see cref="QueryRouter"/> → state results.
/// </summary>
internal sealed class LauncherQuerySession
{
    private readonly QueryRouter _router;
    private readonly LauncherOverlayState _state;
    private CancellationTokenSource? _cts;
    private int _version;

    public LauncherQuerySession(QueryRouter router, LauncherOverlayState state)
    {
        _router = router;
        _state = state;
    }

    public void NotifyTextChanged(string rawInput)
    {
        var match = _router.Match(rawInput);
        _state.ApplyMatch(match, rawInput);

        var version = Interlocked.Increment(ref _version);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = QueryDebouncedAsync(rawInput, version, token);
    }

    public async Task ActivateSelectedAsync(ResultActionKind? actionOverride = null)
    {
        var match = _state.ActiveMatch;
        var item = _state.SelectedItem;
        if (match is null || item is null)
        {
            return;
        }

        if (actionOverride is { } action && action != item.Action)
        {
            item = item with { Action = action };
        }

        await _router.ActivateAsync(match, item).ConfigureAwait(true);
    }

    private async Task QueryDebouncedAsync(string rawInput, int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(120, token).ConfigureAwait(true);
            if (version != Volatile.Read(ref _version))
            {
                return;
            }

            var response = await _router.QueryAsync(rawInput, token).ConfigureAwait(true);
            if (version != Volatile.Read(ref _version) || token.IsCancellationRequested)
            {
                return;
            }

            _state.SetResults(response.Items);
        }
        catch (OperationCanceledException)
        {
            // newer keystroke
        }
    }
}
