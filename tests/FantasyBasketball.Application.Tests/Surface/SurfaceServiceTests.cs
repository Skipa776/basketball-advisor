using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Health;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Surface;

public sealed class SurfaceServiceTests
{
    [Fact]
    public async Task Projection_decomposition_returns_four_sibling_records()
    {
        var playerId = new PlayerId(Guid.NewGuid());
        var baseline = Baseline(playerId);
        var adjusted = new AdjustedProjection(
            Guid.NewGuid(),
            playerId,
            baseline.Id,
            baseline.ProjectedPerGame,
            [],
            0m,
            Confidence.Moderate,
            1m,
            false,
            DateTimeOffset.UnixEpoch);
        var observed = Observed(playerId);
        var value = new FantasyValue(
            playerId,
            Guid.NewGuid(),
            10m,
            700m,
            adjusted.Id);
        var service = new ProjectionDecompositionService(
            new FakeProjectionQuery(observed, baseline, adjusted, value));

        var result = await service.GetAsync(
            playerId,
            value.LeagueId,
            TestContext.Current.CancellationToken);

        result.Observed.ShouldBe(observed);
        result.Baseline.ShouldBe(baseline);
        result.Adjusted.ShouldBe(adjusted);
        result.Value.ShouldBe(value);
    }

    [Fact]
    public async Task Failed_or_stale_source_is_degraded_and_never_throws()
    {
        var now = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var successful = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            now.AddDays(-3),
            now.AddDays(-3),
            1,
            0,
            null);
        var failed = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Failed,
            now.AddHours(-1),
            now.AddHours(-1),
            0,
            0,
            "fixture failure");
        var service = new DataSourceHealthService(
            new FakeImportRunQuery([failed, successful]),
            new FixedTimeProvider(now),
            new DataSourceHealthOptions());

        var health = await service.GetAsync(
            TestContext.Current.CancellationToken);

        var source = health.Single(value =>
            value.Source == DataSourceName.BallDontLie);
        source.LastSuccess.ShouldBe(successful.FinishedAt);
        source.LastFailure.ShouldBe(failed.FinishedAt);
        source.IsStale.ShouldBeTrue();
        source.IsDegraded.ShouldBeTrue();
    }

    private static BaselineProjection Baseline(PlayerId playerId) =>
        new(
            Guid.NewGuid(),
            playerId,
            20m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 0.5m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 20m,
                [StatKey.PTS] = 10m,
            }),
            70,
            DateTimeOffset.UnixEpoch,
            "surface-test-v1");

    private static ObservedStats Observed(PlayerId playerId) =>
        new(
            playerId,
            new SeasonStatLine(
                playerId,
                2026,
                50,
                20m,
                new StatLine(new Dictionary<StatKey, decimal>()),
                new StatLine(new Dictionary<StatKey, decimal>
                {
                    [StatKey.MIN] = 1000m,
                }),
                null,
                new DataProvenance(
                    DataSourceName.Manual,
                    null,
                    DateTimeOffset.UnixEpoch,
                    null,
                    "manual-v1",
                    DataSourceConfidence.ManualEntry,
                    new string('a', 64))),
            DateTimeOffset.UnixEpoch);

    private sealed class FakeProjectionQuery(
        ObservedStats observed,
        BaselineProjection baseline,
        AdjustedProjection adjusted,
        FantasyValue value) : IProjectionQueryRepository
    {
        public Task<ProjectionDecomposition?> GetLatestDecompositionAsync(
            PlayerId playerId,
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProjectionDecomposition?>(
                playerId == baseline.PlayerId && leagueId == value.LeagueId
                    ? new ProjectionDecomposition(
                        observed,
                        baseline,
                        adjusted,
                        value)
                    : null);
    }

    private sealed class FakeImportRunQuery(
        IReadOnlyList<DataImportRun> runs) : IImportRunQueryRepository
    {
        public Task<PagedResult<DataImportRun>> ListAsync(
            int page,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<DataImportRun>(
                runs,
                runs.Count,
                page,
                limit));

        public Task<IReadOnlyList<DataImportRun>> ListRecentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(runs);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
