using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Projections;
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
            new FixedTimeProvider(computedAt));
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
        projections[0].PerMinuteRates[StatKey.PTS].ShouldBe(
            0.4632352941176470588235294118m);
        projections[1].PerMinuteRates[StatKey.PTS].ShouldBe(
            0.2867647058823529411764705882m);
    }

    private static SeasonStatLine CreateSource(
        string externalId,
        int gamesPlayed,
        decimal minutes,
        decimal points) =>
        new(
            new PlayerId(Guid.NewGuid()),
            2026,
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
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
