using System.Globalization;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Api;

/// <summary>
/// The backtest is CLI-only (backtest_harness, row B-10): it reads whole seasons and
/// takes minutes, so no web request can start one.
/// <c>dotnet run --project src/FantasyBasketball.Api -- backtest --eval 2026 [--as-of 2025-10-01] [--out path] [--drafts 200]</c>
/// (<c>--drafts 0</c> skips the draft benchmark).
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
        var report = BacktestReportWriter.Render(result, commit);
        if (result.BenchmarkPool is { } benchmarkPool && Option(args, "--drafts") is not "0")
        {
            report += await BenchmarkAsync(scope.ServiceProvider, result, benchmarkPool,
                int.Parse(Option(args, "--drafts") ?? "200", CultureInfo.InvariantCulture));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, report);
        Console.WriteLine($"Backtest report written to {output}");
        return true;
    }

    /// <summary>
    /// The 2025-26 draft benchmark: board, ADP bot and simulator draft the same seeded 10-team
    /// drafts against ADP-jitter opponents; rosters score their actual starting-lineup points.
    /// </summary>
    private static async Task<string> BenchmarkAsync(IServiceProvider services, ProjectionBacktestResult result,
        IReadOnlyList<BenchmarkPlayer> pool, int drafts)
    {
        var espn = LeagueCatalog.CreateEspnDefaultPointsLeague();
        var league = new FantasyLeague(Guid.NewGuid(), "Benchmark", LeagueType.Points, 10, espn.ScoringRules, [],
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()).RosterSlots, LineupCadence.Daily);
        // No 2025 ADP is stored: the market stand-in is the rank by last season's fantasy points.
        var market = pool.OrderByDescending(player => player.LastSeasonPoints).ThenBy(player => player.PlayerId.Value)
            .Select((player, rank) => (player.PlayerId, Rank: rank + 1)).ToDictionary(pair => pair.PlayerId, pair => (decimal)pair.Rank);
        var candidates = pool.Select(player => new DraftCandidate(player.PlayerId, player.Distribution.Mean, player.Positions,
            market[player.PlayerId], 0m, 0m, 0m, new Dictionary<StatKey, decimal>(), Distribution: player.Distribution)).ToArray();
        var games = await services.GetRequiredService<IGameRepository>().ListScheduledAsync(DataSourceName.BallDontLie,
            new DateTimeOffset(result.EvalSeasonEndYear - 1, 10, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(result.EvalSeasonEndYear, 4, 20, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
        var lineup = new LineupOptimizer(league.RosterSlots, new SeasonSchedule(games), league.Cadence);
        const int Rollouts = 500;
        var benchmark = new DraftBenchmark(services.GetRequiredService<DraftBoard>());
        var comparison = benchmark.Compare(league, candidates, pool.ToDictionary(player => player.PlayerId, player => player.ActualSeasonPoints),
            [("ADP bot", DraftBenchmark.AdpDrafter), ("Heuristic board", benchmark.BoardDrafter),
                ("Simulator", DraftBenchmark.SimulatorDrafter(new DraftSimulator(new SimulationOptions { Rollouts = Rollouts }), lineup))],
            drafts);
        return BacktestReportWriter.RenderBenchmark(comparison,
            $"{comparison.Drafts} seeded 10-team, {lineup.StarterCount}-starter drafts (ESPN points scoring, user slot rotating) over the " +
            $"{pool.Count}-player {result.TrainSeasonEndYear - 1}–{result.TrainSeasonEndYear % 100:00} pool with projections as of " +
            $"{result.AsOf:yyyy-MM-dd}. Opponents take lowest ADP after Normal(0, max(6, 0.2·ADP)) jitter; with no 2025 ADP stored, " +
            "ADP is each player's rank by last season's fantasy points. Each roster scores its best starting lineup's **actual** " +
            $"{result.EvalSeasonEndYear - 1}–{result.EvalSeasonEndYear % 100:00} points. The simulator ran {Rollouts} rollouts per pick.");
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
