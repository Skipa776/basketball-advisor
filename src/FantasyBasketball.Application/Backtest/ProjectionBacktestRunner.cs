using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Backtest;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Modeling;
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
    AccuracyReport Naive,
    string? HierarchicalVersion = null,
    AccuracyReport? Hierarchical = null,
    string? MinutesVersion = null,
    IntervalCoverage? Intervals = null,
    string? DistributionVersions = null);

/// <summary>
/// Scores the production baseline projector, and the "last season repeats" baseline,
/// against a held-out season's actual fantasy points per game (backtest_contract).
/// Training reads go through the as-of guard; the eval season's box scores are the
/// target and are read directly. When a projection-rates model is active, the
/// hierarchical projector is scored on the same players, with the projection-minutes
/// model's minutes when one is active and the baseline's minutes otherwise.
/// </summary>
public sealed class ProjectionBacktestRunner(
    ISeasonStatLineRepository stats,
    IBoxScoreRepository boxScores,
    BaselineProjector projector,
    ProjectionOptions options,
    PointsScoringEngine scoring,
    IModelVersionRepository models,
    IPlayerRepository players)
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
        var hierarchical = await ScoreHierarchicalAsync(evaluated, evalSeasonEndYear, statSource, asOf, cancellationToken);
        return new ProjectionBacktestResult(
            evalSeasonEndYear,
            trainSeason,
            asOf,
            evaluated.Length,
            options.ModelVersion,
            AccuracyMetrics.Compute(model, actual),
            AccuracyMetrics.Compute(naive, actual),
            hierarchical?.Version,
            hierarchical is null ? null : AccuracyMetrics.Compute(hierarchical.Projected, actual),
            hierarchical?.MinutesVersion,
            hierarchical?.Sds is { } sds ? IntervalCoverage.Compute(hierarchical.Projected, sds, actual) : null,
            hierarchical?.DistributionVersions);
    }

    private async Task<HierarchicalScore?> ScoreHierarchicalAsync(
        IReadOnlyList<SeasonStatLine> evaluated,
        int evalSeasonEndYear,
        string statSource,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        if (await ActiveModelAsync(ProjectionRateParameters.ModelName, asOf, cancellationToken) is not { } model)
        {
            return null;
        }

        var minutesModel = await ActiveModelAsync(MinutesModelParameters.ModelName, asOf, cancellationToken);
        var availabilityModel = await ActiveModelAsync(AvailabilityModelParameters.ModelName, asOf, cancellationToken);
        var covarianceModel = await ActiveModelAsync(StatCovarianceParameters.ModelName, asOf, cancellationToken);
        var projector = new SeasonProjector(
            ProjectionRateParameters.Parse(model.ParametersJson),
            minutesModel is null ? null : MinutesModelParameters.Parse(minutesModel.ParametersJson),
            availabilityModel is null ? null : AvailabilityModelParameters.Parse(availabilityModel.ParametersJson),
            covarianceModel is null ? null : StatCovarianceParameters.Parse(covarianceModel.ParametersJson),
            model.Version);
        var guarded = new AsOfSeasonStatLineRepository(stats, asOf);
        var history = new List<SeasonStatLine>();
        for (var lag = 1; lag <= 3; lag++)
        {
            history.AddRange(await guarded.ListPoolAsync(evalSeasonEndYear - lag, statSource, cancellationToken));
        }

        var seasonGames = SeasonProjector.SeasonLengths(history);
        var byPlayer = history.ToLookup(line => line.PlayerId);
        var projected = new decimal[evaluated.Count];
        var sds = new List<decimal?>(evaluated.Count);
        for (var index = 0; index < evaluated.Count; index++)
        {
            var line = evaluated[index];
            var player = await players.GetAsync(line.PlayerId, cancellationToken);
            var projection = projector.Project(
                line.PlayerId,
                evalSeasonEndYear,
                player?.Positions.FirstOrDefault(),
                byPlayer[line.PlayerId].ToArray(),
                seasonGames,
                new MinutesProjector().Project(line.GamesPlayed, line.MinutesPerGame, options));
            projected[index] = scoring.Score(projection.PerGame, League);
            sds.Add(projection.Distribution?.FantasyPointsPerGame(League.ScoringRules).Sd);
        }

        return new HierarchicalScore(model.Version, projected, minutesModel?.Version,
            sds.All(sd => sd is not null) ? sds.Select(sd => sd!.Value).ToArray() : null,
            covarianceModel is null || availabilityModel is null ? null : $"{availabilityModel.Version}, {covarianceModel.Version}");
    }

    private sealed record HierarchicalScore(
        string Version, decimal[] Projected, string? MinutesVersion, decimal[]? Sds, string? DistributionVersions);

    /// <summary>The active version of a model, refusing one trained on seasons not yet complete at the as-of.</summary>
    private async Task<ModelVersion?> ActiveModelAsync(string modelName, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        if (await models.GetActiveAsync(modelName, cancellationToken) is not { } model)
        {
            return null;
        }

        var lastKnown = new DateTimeOffset(model.TrainSeasonEndYears.Max(), 6, 30, 0, 0, 0, TimeSpan.Zero);
        return lastKnown <= asOf
            ? model
            : throw new LeakageException(
                $"Backtest leakage: {model.Version} trained on {model.TrainSeasonEndYears.Max()}, known at {lastKnown:O}, after the as-of {asOf:O}.");
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
