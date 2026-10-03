using System.Globalization;
using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Api;

/// <summary>
/// The backtest is CLI-only (backtest_harness, row B-10): it reads whole seasons and
/// takes minutes, so no web request can start one.
/// <c>dotnet run --project src/FantasyBasketball.Api -- backtest --eval 2026 [--as-of 2025-10-01] [--out path]</c>
/// </summary>
public static class BacktestCommand
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (args is not ["backtest", ..])
        {
            return false;
        }

        var evalSeason = int.Parse(Option(args, "--eval")
            ?? throw new ArgumentException("backtest requires --eval <season end year>."), CultureInfo.InvariantCulture);
        var asOf = Option(args, "--as-of") is { } date
            ? DateTimeOffset.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            : new DateTimeOffset(evalSeason - 1, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var output = RepoPath.Resolve(Option(args, "--out") ?? $"docs/backtest/report-{evalSeason}.md");

        await using var scope = app.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ProjectionBacktestRunner>();
        ProjectionBacktestResult result;
        try
        {
            result = await runner.RunAsync(evalSeason, DataSourceName.BasketballReference, asOf, CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or LeakageException)
        {
            await Console.Error.WriteLineAsync($"Backtest failed: {exception.Message}");
            Environment.ExitCode = 1;
            return true;
        }

        var commit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, BacktestReportWriter.Render(result, commit));
        Console.WriteLine($"Backtest report written to {output}");
        return true;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
