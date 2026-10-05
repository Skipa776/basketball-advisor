using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Application.Tests.Backtest;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Projections;

public sealed class ProjectionServiceTests
{
    [Fact]
    public async Task Pool_projection_persists_one_observed_baseline_pair_per_player()
    {
        var repository = new FakeProjectionRepository();
        var computedAt =
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var options = new ProjectionOptions();
        var service = new ProjectionService(
            new BaselineProjector(new MinutesProjector(), options),
            repository,
            options,
            new FixedTimeProvider(computedAt),
            new FakeModelVersionRepository(),
            new FakeSeasonStatLineRepository([]),
            new FakePlayerRepository());
        var first = CreateSource("first", 40, 1200m, 600m);
        var second = CreateSource("second", 40, 1200m, 300m);

        var projections = await service.ProjectPoolAsync(
            [first, second],
            TestContext.Current.CancellationToken);

        projections.Count.ShouldBe(2);
        repository.Items.Count.ShouldBe(2);
        repository.Items.ShouldAllBe(item => item.Observed.AsOf == computedAt);
        repository.Items.ShouldAllBe(
            item => item.Baseline.ComputedAt == computedAt);
        projections[0].Baseline.PerMinuteRates[StatKey.PTS].ShouldBe(
            0.4632352941176470588235294118m);
        projections[1].Baseline.PerMinuteRates[StatKey.PTS].ShouldBe(
            0.2867647058823529411764705882m);
        projections.ShouldAllBe(item => item.Distribution == null);
        repository.Distributions.ShouldBeEmpty();
    }

    [Fact]
    public async Task P14_active_models_publish_hierarchical_baselines_with_distributions()
    {
        var repository = new FakeProjectionRepository();
        var options = new ProjectionOptions();
        var first = CreateSource("first", 70, 2100m, 1400m);
        var service = new ProjectionService(
            new BaselineProjector(new MinutesProjector(), options),
            repository,
            options,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)),
            new FakeModelVersionRepository([.. ModelFixtures.All()]),
            new FakeSeasonStatLineRepository([]),
            new FakePlayerRepository());

        var projections = await service.ProjectPoolAsync([first], TestContext.Current.CancellationToken);

        var projected = projections.ShouldHaveSingleItem();
        projected.Baseline.ModelVersion.ShouldBe("rates-test+minutes-test+availability-test+covariance-test");
        var distribution = projected.Distribution.ShouldNotBeNull();
        projected.Baseline.ProjectedGamesPlayed.ShouldBe(decimal.ToInt32(decimal.Round(distribution.Games.Mean, 0, MidpointRounding.AwayFromZero)));
        repository.Distributions.ShouldHaveSingleItem().BaselineId.ShouldBe(projected.Baseline.Id);
    }

    [Fact]
    public async Task P17_players_who_sat_out_the_season_are_projected_when_drafters_expect_them_back()
    {
        var repository = new FakeProjectionRepository();
        var options = new ProjectionOptions();
        var active = CreateSource("active", 70, 2100m, 1400m);
        var returning = CreateSource("returning", 60, 2000m, 1300m, 2025);
        var retired = CreateSource("retired", 60, 2000m, 1300m, 2025);
        var lastYearsAdp = CreateSource("stale-adp", 60, 2000m, 1300m, 2025);
        var adp = new FakeAdpRepository(
            Adp(returning.PlayerId, new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero)),
            Adp(lastYearsAdp.PlayerId, new DateTimeOffset(2025, 9, 20, 0, 0, 0, TimeSpan.Zero)));
        var service = new ProjectionService(
            new BaselineProjector(new MinutesProjector(), options),
            repository,
            options,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)),
            new FakeModelVersionRepository([.. ModelFixtures.All()]),
            new SeasonFilteredStatLines([active, returning, retired, lastYearsAdp]),
            new FakePlayerRepository(),
            adp);

        var projections = await service.ProjectPoolAsync([active], TestContext.Current.CancellationToken);

        projections.Select(item => item.Baseline.PlayerId).ShouldBe([active.PlayerId, returning.PlayerId], ignoreOrder: true);
        var comeback = projections.Single(item => item.Baseline.PlayerId == returning.PlayerId);
        comeback.Distribution.ShouldNotBeNull();
        comeback.Baseline.ProjectedMinutesPerGame.ShouldBeGreaterThan(0m);
        repository.Items.Single(item => item.Baseline.PlayerId == returning.PlayerId).Observed.Source.SeasonEndYear.ShouldBe(2025);
    }

    private static Domain.Draft.AdpEntry Adp(PlayerId playerId, DateTimeOffset fetchedAt) =>
        new(Guid.NewGuid(), playerId, 30m, null,
            new DataProvenance(DataSourceName.FantasyPros, null, fetchedAt, null, "fantasypros-v1", DataSourceConfidence.ManualEntry, new string('b', 64)));

    private sealed class FakeAdpRepository(params Domain.Draft.AdpEntry[] entries) : IAdpRepository
    {
        public Task AddAsync(Domain.Draft.AdpEntry entry, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Domain.Draft.AdpEntry?> GetLatestAsync(PlayerId playerId, CancellationToken cancellationToken) =>
            Task.FromResult(entries.FirstOrDefault(entry => entry.PlayerId == playerId));
    }

    /// <summary>Season lines by year, as the real store returns them.</summary>
    private sealed class SeasonFilteredStatLines(IReadOnlyList<SeasonStatLine> lines) : ISeasonStatLineRepository
    {
        public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SeasonProjectionPool>>([]);

        public Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(int seasonEndYear, string source, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SeasonStatLine>>(lines.Where(line => line.SeasonEndYear == seasonEndYear).ToArray());

        public Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveAgeAsync(PlayerId playerId, int seasonEndYear, string source, int age, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SeasonStatLine?> GetAsync(PlayerId playerId, int seasonEndYear, string source, CancellationToken cancellationToken) =>
            Task.FromResult(lines.FirstOrDefault(line => line.PlayerId == playerId && line.SeasonEndYear == seasonEndYear));
    }

    private static SeasonStatLine CreateSource(
        string externalId,
        int gamesPlayed,
        decimal minutes,
        decimal points,
        int seasonEndYear = 2026) =>
        new(
            new PlayerId(Guid.NewGuid()),
            seasonEndYear,
            gamesPlayed,
            minutes / gamesPlayed,
            new StatLine(new Dictionary<StatKey, decimal>()),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = minutes,
                [StatKey.PTS] = points,
            }),
            null,
            new DataProvenance(
                DataSourceName.Manual,
                externalId,
                DateTimeOffset.UnixEpoch,
                null,
                "manual-v1",
                DataSourceConfidence.ManualEntry,
                new string('a', 64)));

    private sealed class FakeProjectionRepository : IProjectionRepository
    {
        public List<(ObservedStats Observed, BaselineProjection Baseline)> Items
        {
            get;
        } = [];

        public Task AddAsync(
            ObservedStats observed,
            BaselineProjection baseline,
            CancellationToken cancellationToken)
        {
            Items.Add((observed, baseline));
            return Task.CompletedTask;
        }

        public Task<ObservedStats?> GetLatestObservedAsync(
            PlayerId playerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items
                .Where(item => item.Observed.PlayerId == playerId)
                .Select(item => item.Observed)
                .LastOrDefault());

        public Task<BaselineProjection?> GetBaselineAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items
                .Where(item => item.Baseline.Id == id)
                .Select(item => item.Baseline)
                .SingleOrDefault());

        public Task AddDistributionAsync(Guid baselineProjectionId, ProjectionDistribution distribution, CancellationToken cancellationToken)
        {
            Distributions.Add((baselineProjectionId, distribution));
            return Task.CompletedTask;
        }

        public List<(Guid BaselineId, ProjectionDistribution Distribution)> Distributions { get; } = [];

        public Task AddAdjustedAsync(
            AdjustedProjection adjusted,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<AdjustedProjection?> GetAdjustedAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<AdjustedProjection?>(null);

        public Task AddFantasyValueAsync(
            FantasyValue value,
            FantasyBasketball.Domain.Leagues.FantasyLeague league,
            DateTimeOffset computedAt,
        Guid? publicationId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<FantasyValue?> GetFantasyValueAsync(
            PlayerId playerId,
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FantasyValue?>(null);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
