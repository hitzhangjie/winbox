using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Prefix <c>&gt;</c> routes to a shell command (cmd.exe). Settings / working directory later.
/// </summary>
public sealed class ShellPlugin : IWinBoxPlugin, IQueryHandler
{
    public const int MatchPriority = 90;

    public string Id => "winbox.shell";
    public string Name => "Shell";
    public string Version => "0.1.0";
    public string HandlerId => Id;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public bool TryMatch(string rawInput, [NotNullWhen(true)] out QueryMatch? match)
    {
        match = null;
        if (string.IsNullOrEmpty(rawInput) || rawInput[0] != '>')
        {
            return false;
        }

        var payload = rawInput.Length > 1 ? rawInput[1..].TrimStart() : string.Empty;
        match = new QueryMatch(
            HandlerId,
            MatchPriority,
            Prefix: ">",
            Payload: payload,
            ModeLabel: "CMD");
        return true;
    }

    public Task<QueryResponse> QueryAsync(QueryMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(match.Payload))
        {
            return Task.FromResult(new QueryResponse(
            [
                new QueryResultItem("shell-hint", "Type a command to run in cmd.exe", Action: ResultActionKind.None, IconKey: ResultIconKeys.Shell),
            ]));
        }

        var item = new QueryResultItem(
            Id: "shell-run",
            Title: match.Payload,
            Subtitle: "Run in cmd.exe",
            Payload: match.Payload,
            Action: ResultActionKind.RunCommand,
            IconKey: ResultIconKeys.Shell);
        return Task.FromResult(new QueryResponse([item]));
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        var command = item.Payload ?? match.Payload;
        if (string.IsNullOrWhiteSpace(command))
        {
            return Task.CompletedTask;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/k " + command,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }
}
