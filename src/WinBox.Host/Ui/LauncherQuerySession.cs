using System.Runtime.InteropServices;
using System.Windows;
using WinBox.Abstractions;
using WinBox.Host.Query;

namespace WinBox.Host.Ui;

/// <summary>
/// Debounced bridge from overlay text → <see cref="QueryRouter"/> → state results.
/// AI streaming is Enter-triggered via <see cref="ResultActionKind.Submit"/>.
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

        if (item.Action == ResultActionKind.Submit)
        {
            await RunProgressiveSubmitAsync(match).ConfigureAwait(true);
            return;
        }

        if (item.Action == ResultActionKind.CopyText
            && !string.IsNullOrEmpty(item.Payload))
        {
            TryCopyText(item.Payload);
        }

        await _router.ActivateAsync(match, item).ConfigureAwait(true);
    }

    /// <summary>
    /// Whether Enter / double-click should dismiss after activate.
    /// <see cref="ResultActionKind.None"/> (streaming) and <see cref="ResultActionKind.Submit"/> stay open.
    /// </summary>
    public static bool ShouldDismissAfterActivate(ResultActionKind? action) =>
        action is not null
            and not ResultActionKind.None
            and not ResultActionKind.Submit;

    private async Task RunProgressiveSubmitAsync(QueryMatch match)
    {
        var version = Interlocked.Increment(ref _version);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await _router.RunProgressiveAsync(
                    match,
                    snapshot =>
                    {
                        if (version != Volatile.Read(ref _version) || token.IsCancellationRequested)
                        {
                            return;
                        }

                        _state.SetResults(snapshot.Items);
                    },
                    token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // newer keystroke or re-submit
        }
    }

    private static void TryCopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            // Clipboard busy / unavailable — activation still proceeds.
        }
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

            await _router.RunQueryAsync(
                    rawInput,
                    snapshot =>
                    {
                        if (version != Volatile.Read(ref _version) || token.IsCancellationRequested)
                        {
                            return;
                        }

                        _state.SetResults(snapshot.Items);
                    },
                    token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // newer keystroke
        }
    }
}
