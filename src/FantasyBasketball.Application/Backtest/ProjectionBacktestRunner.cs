using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Backtest;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Players;
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
    string? DistributionVersions = null,
    IReadOnlyList<BenchmarkPlayer>? BenchmarkPool = null);

/// <summary>
/// A training-pool player for the draft benchmark: his hierarchical season-value distribution,
/// his actual eval-season fantasy points (0 if he never played) and last season's points.
/// </summary>
public sealed record BenchmarkPlayer(
    PlayerId PlayerId,
    IReadOnlyList<string> Positions,
    SeasonValueDistribution Distribution,
    decimal ActualSeasonPoints,
    decimal LastSeasonPoints);

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

        var seasons = await ActualSeasonsAsync(evalSeasonEndYear, cancellationToken);
        var actuals = seasons.Where(pair => pair.Value.Games >= MinEvalGames)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Points / pair.Value.Games);
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
        var hierarchical = await ScoreHierarchicalAsync(pool, evaluated, seasons, evalSeasonEndYear, statSource, asOf, cancellationToken);
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
            hierarchical?.DistributionVersions,
            hierarchical?.Benchmark);
    }

    private async Task<HierarchicalScore?> ScoreHierarchicalAsync(
        IReadOnlyList<SeasonStatLine> pool,
        IReadOnlyList<SeasonStatLine> evaluated,
        IReadOnlyDictionary<Guid, (int Games, decimal Points)> seasons,
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
        var projections = new Dictionary<PlayerId, (SeasonProjection Projection, IReadOnlyList<string> Positions)>();
        foreach (var line in pool)
        {
            var player = await players.GetAsync(line.PlayerId, cancellationToken);
            projections[line.PlayerId] = (projector.Project(
                line.PlayerId,
                evalSeasonEndYear,
                player?.Positions.FirstOrDefault(),
                byPlayer[line.PlayerId].ToArray(),
                seasonGames,
                new MinutesProjector().Project(line.GamesPlayed, line.MinutesPerGame, options)), player?.Positions ?? []);
        }

        var projected = evaluated.Select(line => scoring.Score(projections[line.PlayerId].Projection.PerGame, League)).ToArray();
        var sds = evaluated.Select(line => projections[line.PlayerId].Projection.Distribution?.FantasyPointsPerGame(League.ScoringRules).Sd).ToArray();
        var benchmark = pool.All(line => projections[line.PlayerId].Projection.Distribution is not null)
            ? pool.Select(line =>
            {
                var (projection, positions) = projections[line.PlayerId];
                var (_, sd) = projection.Distribution!.FantasyPointsPerGame(League.ScoringRules);
                return new BenchmarkPlayer(line.PlayerId, positions,
                    new SeasonValueDistribution(scoring.Score(projection.PerGame, League), sd, projection.Distribution.Games),
                    seasons.TryGetValue(line.PlayerId.Value, out var season) ? season.Points : 0m,
                    scoring.Score(line.Totals, League));
            }).ToArray()
            : null;
        return new HierarchicalScore(model.Version, projected, minutesModel?.Version,
            sds.All(sd => sd is not null) ? sds.Select(sd => sd!.Value).ToArray() : null,
            covarianceModel is null || availabilityModel is null ? null : $"{availabilityModel.Version}, {covarianceModel.Version}",
            benchmark);
    }

    private sealed record HierarchicalScore(
        string Version, decimal[] Projected, string? MinutesVersion, decimal[]? Sds, string? DistributionVersions,
        IReadOnlyList<BenchmarkPlayer>? Benchmark);

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

    /// <summary>Each player's eval-season regular-season games and fantasy points (ESPN scoring).</summary>
    private async Task<Dictionary<Guid, (int Games, decimal Points)>> ActualSeasonsAsync(
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
            .ToDictionary(
                games => games.Key,
                games => (games.Count(), games.Sum(sample => scoring.Score(sample.Statistics!, League))));
    }
}
