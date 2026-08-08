using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using WinBox.Abstractions;

namespace WinBox.Toolbox;

/// <summary>
/// Evaluates simple arithmetic in the launcher (+ - * / and parentheses).
/// </summary>
public sealed class CalculatorPlugin : IWinBoxPlugin, IQueryHandler
{
    public const int MatchPriority = 50;

    private static readonly Regex AllowedChars = new(
        @"^[\d\s+\-*/().]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasOperator = new(
        @"[+\-*/]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Id => "winbox.calculator";
    public string Name => "Calculator";
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
        if (!TryEvaluate(rawInput, out _))
        {
            return false;
        }

        match = new QueryMatch(
            HandlerId,
            MatchPriority,
            Prefix: string.Empty,
            Payload: rawInput.Trim(),
            ModeLabel: null);
        return true;
    }

    public Task<QueryResponse> QueryAsync(QueryMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryEvaluate(match.Payload, out var value))
        {
            return Task.FromResult(new QueryResponse([]));
        }

        var text = value.ToString("G15", CultureInfo.InvariantCulture);
        var item = new QueryResultItem(
            Id: "calc-result",
            Title: text,
            Subtitle: match.Payload,
            Payload: text,
            Action: ResultActionKind.CopyText);
        return Task.FromResult(new QueryResponse([item]));
    }

    public Task ActivateAsync(
        QueryMatch match,
        QueryResultItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public static bool TryEvaluate(string? expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var trimmed = expression.Trim();
        if (!AllowedChars.IsMatch(trimmed) || !HasOperator.IsMatch(trimmed))
        {
            return false;
        }

        try
        {
            var value = new DataTable().Compute(trimmed, null);
            if (value is DBNull)
            {
                return false;
            }

            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }
        catch (EvaluateException)
        {
            return false;
        }
        catch (SyntaxErrorException)
        {
            return false;
        }
    }
}
