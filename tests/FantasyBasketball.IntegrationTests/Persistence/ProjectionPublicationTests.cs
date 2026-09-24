using System.Text.Json;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task P11_publication_keeps_leagues_runs_observations_and_scoring_profiles_coherent()
    {
        var token = TestContext.Current.CancellationToken;
        var first = new PlayerId(Guid.NewGuid());
        var second = new PlayerId(Guid.NewGuid());
        var league = PointsLeague(1m);
        var otherLeague = PointsLeague(2m);
        await using var database = CreateDatabase();
        database.Players.AddRange(
            PlayerRow.Create(first.Value, "First", "first", ["PG"], null),
            PlayerRow.Create(second.Value, "Second", "second", ["C"], null));
        await database.SaveChangesAsync(token);
        var leagues = new LeagueRepository(database);
        await leagues.AddAsync(league, token);
        await leagues.AddAsync(otherLeague, token);
        var statistics = new SeasonStatLineRepository(database);
        await statistics.AddAsync(ProjectionSource(first, 2025, 800m, DataSourceName.BasketballReference), token);
        await statistics.AddAsync(ProjectionSource(second, 2025, 400m, DataSourceName.BasketballReference), token);
        await statistics.AddAsync(ProjectionSource(first, 2025, 1200m, DataSourceName.Manual), token);
        await statistics.AddAsync(ProjectionSource(first, 2026, 1600m, DataSourceName.Manual), token);
        var pools = await statistics.ListPoolsAsync(token);
        pools.Count.ShouldBe(3);
        pools[0].SeasonEndYear.ShouldBe(2026);
        var repository = new ProjectionRepository(database);
        var initialTime = DateTimeOffset.UnixEpoch.AddDays(1);
        var initial = await PublicationService(database, initialTime).RecalculateAsync(
            league.Id, 2025, DataSourceName.BasketballReference, token);
        initial.PlayerCount.ShouldBe(2);
        var original = await repository.GetLatestDecompositionAsync(first, league.Id, token);
        original.ShouldNotBeNull();
        original.Observed.Source.Provenance.Source.ShouldBe(DataSourceName.Manual);
        original.Observed.Source.SeasonEndYear.ShouldBe(2025);
        original.Observed.Source.Totals[StatKey.PTS].ShouldBe(1200m);
        var baselineBytes = JsonSerializer.Serialize(await database.BaselineProjections.AsNoTracking()
            .SingleAsync(row => row.Id == original.Baseline.Id, token));

        // A newer, unrelated league run must not replace this league's source chain.
        await PublicationService(database, initialTime.AddDays(1)).RecalculateAsync(
            otherLeague.Id, 2026, DataSourceName.Manual, token);
        (await repository.GetLatestDecompositionAsync(first, league.Id, token))!.Baseline.Id.ShouldBe(original.Baseline.Id);
        var other = await repository.GetLatestDecompositionAsync(first, otherLeague.Id, token);
        other!.Observed.Source.SeasonEndYear.ShouldBe(2026);
        other.Value.PerGame.ShouldBe(decimal.Round(other.Adjusted.ProjectedPerGame[StatKey.PTS] * 2m, 4, MidpointRounding.AwayFromZero));

        // An older row with a deliberately larger random ID must never win recency.
        database.FantasyValues.Add(FantasyValueRow.Create(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            first.Value, league.Id, 999m, 999m, original.Adjusted.Id, initialTime.AddDays(-1), "obsolete"));
        await database.SaveChangesAsync(token);
        (await repository.GetFantasyValueAsync(first, league.Id, token))!.PerGame.ShouldBe(original.Value.PerGame);

        var replacement = new FantasyLeague(league.Id, league.Name, LeagueType.Points, league.TeamCount,
            [new ScoringRule(StatKey.PTS, 3m)], [], league.RosterSlots, league.Cadence);
        await leagues.SaveScoringAsync(replacement, token);
        (await repository.GetFantasyValueAsync(first, league.Id, token)).ShouldBeNull();
        (await new DraftCandidateRepository(database).ListAsync(league.Id, token)).ShouldBeEmpty();
        (await repository.GetLatestDecompositionAsync(first, league.Id, token)).ShouldBeNull();
        (await repository.GetLatestDecompositionAsync(first, otherLeague.Id, token)).ShouldNotBeNull();

        // Selecting a different pool removes absent players instead of mixing seasons.
        await PublicationService(database, initialTime.AddDays(2)).RecalculateAsync(
            league.Id, 2026, DataSourceName.Manual, token);
        var candidates = await new DraftCandidateRepository(database).ListAsync(league.Id, token);
        candidates.Count.ShouldBe(1);
        candidates[0].PlayerId.ShouldBe(first);

        // League-specific eligibility reaches this league's draft only.
        var primary = candidates[0].Positions;
        await new LeagueEligibilityRepository(database).SaveAsync(league.Id,
            new Dictionary<PlayerId, IReadOnlyList<string>> { [first] = ["PG", "SG"] }, token);
        (await new DraftCandidateRepository(database).ListAsync(league.Id, token))[0].Positions.ShouldBe(["PG", "SG"]);
        (await new DraftCandidateRepository(database).ListAsync(otherLeague.Id, token))
            .Single(candidate => candidate.PlayerId == first).Positions.ShouldBe(primary);
        (await repository.GetFantasyValueAsync(second, league.Id, token)).ShouldBeNull();
        var latest = (await repository.GetLatestDecompositionAsync(first, league.Id, token))!;
        latest.Value.PerGame.ShouldBe(120m);
        latest.Observed.Source.SeasonEndYear.ShouldBe(2026);
        latest.Baseline.Id.ShouldNotBe(original.Baseline.Id);
        JsonSerializer.Serialize(await database.BaselineProjections.AsNoTracking()
            .SingleAsync(row => row.Id == original.Baseline.Id, token)).ShouldBe(baselineBytes);
    }

    [Fact]
    public async Task P12_failed_publication_rolls_back_all_projection_records()
    {
        var token = TestContext.Current.CancellationToken;
        var player = new PlayerId(Guid.NewGuid());
        var league = PointsLeague(9999m);
        await using (var database = CreateDatabase())
        {
            database.Players.Add(PlayerRow.Create(player.Value, "Overflow fixture", "overflow fixture", ["PG"], null));
            await database.SaveChangesAsync(token);
            await new LeagueRepository(database).AddAsync(league, token);
            await new SeasonStatLineRepository(database).AddAsync(ProjectionSource(player, 2026, 800000m, DataSourceName.Manual), token);
            await Should.ThrowAsync<DbUpdateException>(() => PublicationService(database, DateTimeOffset.UnixEpoch)
                .RecalculateAsync(league.Id, 2026, DataSourceName.Manual, token));
        }

        await using var check = CreateDatabase();
        (await check.ObservedStats.CountAsync(token)).ShouldBe(0);
        (await check.BaselineProjections.CountAsync(token)).ShouldBe(0);
        (await check.AdjustedProjections.CountAsync(token)).ShouldBe(0);
        (await check.FantasyValues.CountAsync(token)).ShouldBe(0);
        await Should.ThrowAsync<ResourceConflictException>(() => PublicationService(check, DateTimeOffset.UnixEpoch)
            .RecalculateAsync(league.Id, 2024, DataSourceName.Manual, token));
    }

    private static FantasyLeague PointsLeague(decimal points) => new(Guid.NewGuid(), "Publication fixture", LeagueType.Points,
        7, [new ScoringRule(StatKey.PTS, points)], [], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);

    private static SeasonStatLine ProjectionSource(PlayerId player, int year, decimal points, string source) => new(
        player, year, 40, 30m, new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = points / 40m }),
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.MIN] = 1200m, [StatKey.PTS] = points }), null,
        new DataProvenance(source, null, DateTimeOffset.UnixEpoch, null, $"{source}-v1", 1m, new string('a', 64)));

    private static LeagueProjectionService PublicationService(FantasyDbContext database, DateTimeOffset time)
    {
        var options = new ProjectionOptions();
        var clock = new FixedTimeProvider(time);
        var repository = new ProjectionRepository(database);
        return new LeagueProjectionService(new LeagueRepository(database), new SeasonStatLineRepository(database),
            new ProjectionService(new BaselineProjector(new MinutesProjector(), options), repository, options, clock),
            repository, new ContextEventRepository(database), new ContextApplier(new ConfidenceCalculator()),
            new PointsScoringEngine(), new EfImportTransaction(database), clock);
    }
}
