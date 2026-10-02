using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Backtest;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Backtest;

public sealed record ProjectionBacktestResult(
    int EvalSeasonEndYear,
    int TrainSeasonEndYear,
    DateTimeOffset AsOf,
    int PlayerCount,
    string ModelVersion,
    AccuracyReport Model,
    AccuracyReport Naive);

/// <summary>
/// Scores the production baseline projector, and the "last season repeats" baseline,
/// against a held-out season's actual fantasy points per game (backtest_contract).
/// Training reads go through the as-of guard; the eval season's box scores are the
/// target and are read directly.
/// </summary>
public sealed class ProjectionBacktestRunner(
    ISeasonStatLineRepository stats,
    IBoxScoreRepository boxScores,
    BaselineProjector projector,
    ProjectionOptions options,
    PointsScoringEngine scoring)
{
    public const int MinEvalGames = 20;
    private static readonly FantasyLeague League = LeagueCatalog.CreateEspnDefaultPointsLeague();

    public async Task<ProjectionBacktestResult> RunAsync(
        int evalSeasonEndYear,
        string statSource,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var trainSeason = evalSeasonEndYear - 1;
        var pool = await new AsOfSeasonStatLineRepository(stats, asOf)
            .ListPoolAsync(trainSeason, statSource, cancellationToken);
        if (pool.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {statSource} season lines for {trainSeason}; import that season before backtesting {evalSeasonEndYear}.");
        }

        var actuals = await ActualPointsPerGameAsync(evalSeasonEndYear, cancellationToken);
        var leagueAverageRates = LeagueAverageRateCalculator.Calculate(pool, options);
        var evaluated = pool
            .Where(line => actuals.ContainsKey(line.PlayerId.Value))
            .OrderBy(line => line.PlayerId.Value)
            .ToArray();
        var actual = evaluated.Select(line => actuals[line.PlayerId.Value]).ToArray();
        var model = evaluated
            .Select(line => scoring.Score(
                projector.Project(Guid.NewGuid(), new ObservedStats(line.PlayerId, line, asOf), leagueAverageRates)
                    .ProjectedPerGame,
                League))
            .ToArray();
        var naive = evaluated.Select(line => scoring.Score(line.PerGame, League)).ToArray();
        return new ProjectionBacktestResult(
            evalSeasonEndYear,
            trainSeason,
            asOf,
            evaluated.Length,
            options.ModelVersion,
            AccuracyMetrics.Compute(model, actual),
            AccuracyMetrics.Compute(naive, actual));
    }

    private async Task<Dictionary<Guid, decimal>> ActualPointsPerGameAsync(
        int evalSeasonEndYear,
        CancellationToken cancellationToken)
    {
        var samples = await boxScores.ListAsync(
            evalSeasonEndYear,
            DataSourceName.BasketballReference,
            NbaGamePhase.RegularSeason,
            new DateOnly(evalSeasonEndYear, 6, 30),
            cancellationToken);
        return samples
            .Where(sample => sample is { IsFinal: true, DidPlay: true, Statistics: not null })
            .GroupBy(sample => sample.PlayerId.Value)
            .Where(games => games.Count() >= MinEvalGames)
            .ToDictionary(
                games => games.Key,
                games => games.Average(sample => scoring.Score(sample.Statistics!, League)));
    }
}
