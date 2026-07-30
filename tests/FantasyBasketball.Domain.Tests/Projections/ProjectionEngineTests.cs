using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Projections;

public sealed class ProjectionEngineTests
{
    [Fact]
    public void P01_worked_example_reproduces_exactly()
    {
        var source = CreateWorkedExampleSource();
        var observed = new ObservedStats(
            source.PlayerId,
            source,
            source.Provenance.FetchedAt);
        var leagueRates = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 0.50m,
            [StatKey.REB] = 0.18m,
            [StatKey.AST] = 0.12m,
            [StatKey.STL] = 0.03m,
            [StatKey.BLK] = 0.02m,
            [StatKey.TOV] = 0.07m,
        });
        var baseline = new BaselineProjector(
            new MinutesProjector(),
            new ProjectionOptions())
            .Project(Guid.NewGuid(), observed, leagueRates);
        var perGameValue = new PointsScoringEngine().Score(
            baseline.ProjectedPerGame,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()));
        var value = new FantasyValue(
            source.PlayerId,
            Guid.NewGuid(),
            perGameValue,
            perGameValue * baseline.ProjectedGamesPlayed,
            null);

        baseline.PerMinuteRates[StatKey.PTS].ShouldBe(0.575m);
        baseline.PerMinuteRates[StatKey.REB].ShouldBe(0.195m);
        baseline.ProjectedMinutesPerGame.ShouldBe(30m);
        baseline.ProjectedPerGame[StatKey.PTS].ShouldBe(17.25m);
        baseline.ProjectedPerGame[StatKey.REB].ShouldBe(5.85m);
        baseline.ProjectedPerGame[StatKey.AST].ShouldBe(4.275m);
        baseline.ProjectedPerGame[StatKey.STL].ShouldBe(1.125m);
        baseline.ProjectedPerGame[StatKey.BLK].ShouldBe(0.6m);
        baseline.ProjectedPerGame[StatKey.TOV].ShouldBe(2.775m);
        baseline.ProjectedGamesPlayed.ShouldBe(60);
        value.PerGame.ShouldBe(33.0825m);
        value.SeasonTotal.ShouldBe(1984.9500m);
    }

    [Fact]
    public void P03_ratios_are_recomputed_from_projected_components()
    {
        var source = CreateSource(
            gamesPlayed: 40,
            minutesPerGame: 30m,
            new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1200m,
                [StatKey.FGM] = 300m,
                [StatKey.FGA] = 600m,
                [StatKey.FTM] = 100m,
                [StatKey.FTA] = 125m,
                [StatKey.FG3M] = 80m,
                [StatKey.FG3A] = 200m,
            });
        var leagueRates = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 0.2m,
            [StatKey.FGA] = 0.5m,
            [StatKey.FTM] = 0.1m,
            [StatKey.FTA] = 0.15m,
            [StatKey.FG3M] = 0.05m,
            [StatKey.FG3A] = 0.15m,
        });

        var baseline = Project(source, leagueRates);

        baseline.ProjectedPerGame.Values.Keys.ShouldNotContain(StatKey.FG_PCT);
        baseline.ProjectedPerGame[StatKey.FG_PCT].ShouldBe(
            baseline.ProjectedPerGame[StatKey.FGM]
            / baseline.ProjectedPerGame[StatKey.FGA]);
        baseline.ProjectedPerGame[StatKey.FT_PCT].ShouldBe(
            baseline.ProjectedPerGame[StatKey.FTM]
            / baseline.ProjectedPerGame[StatKey.FTA]);
        baseline.ProjectedPerGame[StatKey.FG3_PCT].ShouldBe(
            baseline.ProjectedPerGame[StatKey.FG3M]
            / baseline.ProjectedPerGame[StatKey.FG3A]);
    }

    [Fact]
    public void P04_same_inputs_are_pure_and_deterministic()
    {
        var source = CreateWorkedExampleSource();
        var rates = LeagueAverageRateCalculator.Calculate(
            [source],
            new ProjectionOptions());

        var baselineId = Guid.NewGuid();
        var first = Project(source, rates, baselineId);
        var second = Project(source, rates, baselineId);

        first.Id.ShouldBe(second.Id);
        first.PlayerId.ShouldBe(second.PlayerId);
        first.ProjectedMinutesPerGame.ShouldBe(second.ProjectedMinutesPerGame);
        first.PerMinuteRates.Values.ShouldBe(second.PerMinuteRates.Values);
        first.ProjectedPerGame.Values.ShouldBe(second.ProjectedPerGame.Values);
        first.ProjectedGamesPlayed.ShouldBe(second.ProjectedGamesPlayed);
        first.ModelVersion.ShouldBe(second.ModelVersion);
    }

    [Fact]
    public void P10_zero_prior_minutes_uses_league_rates_without_division()
    {
        var source = CreateSource(
            gamesPlayed: 0,
            minutesPerGame: 0m,
            new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 0m,
            });
        var leagueRates = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 0.5m,
        });

        var baseline = Project(source, leagueRates);

        baseline.PerMinuteRates[StatKey.PTS].ShouldBe(0.5m);
        baseline.ProjectedMinutesPerGame.ShouldBe(20m);
        baseline.ProjectedPerGame[StatKey.PTS].ShouldBe(10m);
        baseline.ProjectedGamesPlayed.ShouldBe(35);
    }

    private static BaselineProjection Project(
        SeasonStatLine source,
        StatLine leagueRates,
        Guid? baselineId = null) =>
        new BaselineProjector(new MinutesProjector(), new ProjectionOptions())
            .Project(
                baselineId ?? Guid.NewGuid(),
                new ObservedStats(
                    source.PlayerId,
                    source,
                    source.Provenance.FetchedAt),
                leagueRates);

    private static SeasonStatLine CreateWorkedExampleSource() =>
        CreateSource(
            gamesPlayed: 50,
            minutesPerGame: 30m,
            new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1500m,
                [StatKey.PTS] = 900m,
                [StatKey.REB] = 300m,
                [StatKey.AST] = 225m,
                [StatKey.STL] = 60m,
                [StatKey.BLK] = 30m,
                [StatKey.TOV] = 150m,
            });

    private static SeasonStatLine CreateSource(
        int gamesPlayed,
        decimal minutesPerGame,
        IReadOnlyDictionary<StatKey, decimal> totals) =>
        new(
            new PlayerId(Guid.NewGuid()),
            2026,
            gamesPlayed,
            minutesPerGame,
            new StatLine(new Dictionary<StatKey, decimal>()),
            new StatLine(totals),
            null,
            new DataProvenance(
                DataSourceName.Manual,
                "player",
                DateTimeOffset.UnixEpoch,
                null,
                "manual-v1",
                DataSourceConfidence.ManualEntry,
                new string('a', 64)));
}
