using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task BS07_completed_games_round_trip_as_shared_reference_data()
    {
        // An ingestion worker has no signed-in user. Both tables must remain shared.
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        var snapshot = BoxScore(game, players);
        var repository = new BoxScoreRepository(database);
        (await repository.AddAsync(snapshot, TestContext.Current.CancellationToken)).ShouldBeTrue();
        database.ChangeTracker.Clear();

        var samples = await ReadBoxScore(repository);
        samples.Count.ShouldBe(2);
        var played = samples.Single(sample => sample.DidPlay);
        played.GameId.ShouldBe(game);
        played.PlayerId.Value.ShouldBe(players[0]);
        played.SeasonEndYear.ShouldBe(snapshot.SeasonEndYear);
        played.PlayedOn.ShouldBe(snapshot.PlayedOn);
        played.IsFinal.ShouldBeTrue();
        played.Provenance.ShouldBe(snapshot.Provenance);
        played.Statistics![StatKey.PTS].ShouldBe(0m);
        played.Statistics[StatKey.MIN].ShouldBe(1.0167m);
        samples.Single(sample => !sample.DidPlay).Statistics.ShouldBeNull();
        var stored = await database.BoxScoreSnapshots.SingleAsync(TestContext.Current.CancellationToken);
        stored.Phase.ShouldBe(NbaGamePhase.RegularSeason.ToString());
        stored.RawRecordHash.ShouldBe(snapshot.Provenance.RawRecordHash);
        stored.SourceTimestamp.ShouldBe(snapshot.Provenance.SourceTimestamp);
        stored.ExternalId.ShouldBe(snapshot.Provenance.ExternalId);
        foreach (var type in new[] { typeof(BoxScoreSnapshotRow), typeof(PlayerGameStatRow) })
        {
            var entity = database.Model.FindEntityType(type)!;
            entity.FindProperty("OwnerId").ShouldBeNull();
            foreach (var field in new[] { "Source", "FetchedAt", "ParserVersion", "Confidence", "RawRecordHash" })
                entity.FindProperty(field)!.IsNullable.ShouldBeFalse();
            entity.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
        }
    }

    [Fact]
    public async Task BS08_repeated_and_concurrent_snapshots_are_idempotent()
    {
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        var snapshot = BoxScore(game, players);
        var concurrentOptions = new DbContextOptionsBuilder<FantasyDbContext>(options)
            .AddInterceptors(new ConcurrentBoxScoreSaves()).Options;
        await using var first = new FantasyDbContext(concurrentOptions);
        await using var second = new FantasyDbContext(concurrentOptions);
        var results = await Task.WhenAll(
            new BoxScoreRepository(first).AddAsync(snapshot, TestContext.Current.CancellationToken),
            new BoxScoreRepository(second).AddAsync(snapshot, TestContext.Current.CancellationToken));
        results.Count(inserted => inserted).ShouldBe(1);
        (await new BoxScoreRepository(database).AddAsync(snapshot, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await database.BoxScoreSnapshots.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await database.PlayerGameStats.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task BS09_corrections_replace_whole_snapshots_without_blending_sources_or_seasons()
    {
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        var repository = new BoxScoreRepository(database);
        await repository.AddAsync(BoxScore(game, players), TestContext.Current.CancellationToken);
        await repository.AddAsync(BoxScore(game, players[..1], revision: 1, points: 25), TestContext.Current.CancellationToken);
        var current = await ReadBoxScore(repository);
        current.Count.ShouldBe(1); // The removed DNP must not leak from the older page.
        current[0].Statistics![StatKey.PTS].ShouldBe(25m);
        current[0].Provenance.RawRecordHash.ShouldBe(new string('b', 64));
        await repository.AddAsync(BoxScore(game, players[..1], source: DataSourceName.BasketballReference, points: 80),
            TestContext.Current.CancellationToken);
        (await ReadBoxScore(repository))[0].Statistics![StatKey.PTS].ShouldBe(25m);
        (await repository.ListAsync(2026, DataSourceName.BasketballReference, NbaGamePhase.RegularSeason,
            new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken))[0].Statistics![StatKey.PTS].ShouldBe(80m);
        (await repository.ListAsync(2025, DataSourceName.Manual, NbaGamePhase.RegularSeason,
            new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await repository.ListAsync(2026, DataSourceName.Manual, NbaGamePhase.RegularSeason,
            new DateOnly(2026, 1, 1), TestContext.Current.CancellationToken)).ShouldBeEmpty();

        // A later classification overrides the old regular-season observation, even with the same page hash.
        await repository.AddAsync(BoxScore(game, players[..1], revision: 1, phase: NbaGamePhase.Playoffs, fetchedDay: 5),
            TestContext.Current.CancellationToken);
        (await ReadBoxScore(repository)).ShouldBeEmpty();
        (await repository.ListPoolsAsync(TestContext.Current.CancellationToken))
            .ShouldAllBe(pool => pool.Source == DataSourceName.BasketballReference);
        (await repository.ListAsync(2026, DataSourceName.Manual, NbaGamePhase.Playoffs,
            new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        await repository.AddAsync(BoxScore(game, players[..1], revision: 2, fetchedDay: 6, playedOn: new DateOnly(2026, 2, 1)),
            TestContext.Current.CancellationToken);
        (await ReadBoxScore(repository)).ShouldBeEmpty(); // Date correction also cannot resurrect the old page.
        (await database.BoxScoreSnapshots.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(5);
        (await database.PlayerGameStats.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(6);
    }

    [Fact]
    public async Task BS10_invalid_player_rolls_back_entire_snapshot_and_context_can_retry()
    {
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        var missing = Guid.NewGuid();
        var repository = new BoxScoreRepository(database);
        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(
            BoxScore(game, [players[0], missing]), TestContext.Current.CancellationToken));
        (await database.BoxScoreSnapshots.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.PlayerGameStats.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        database.ChangeTracker.Entries<BoxScoreSnapshotRow>().ShouldBeEmpty();
        database.ChangeTracker.Entries<PlayerGameStatRow>().ShouldBeEmpty();
        (await repository.AddAsync(BoxScore(game, players), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task BS10_cancellation_and_invalid_schedule_reference_publish_nothing()
    {
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        var repository = new BoxScoreRepository(database);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => repository.AddAsync(BoxScore(game, players), cancellation.Token));
        await Should.ThrowAsync<ArgumentException>(() => repository.AddAsync(
            BoxScore(Guid.NewGuid(), players), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => repository.AddAsync(
            BoxScore(game, players, season: 2025), TestContext.Current.CancellationToken));
        (await database.BoxScoreSnapshots.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.PlayerGameStats.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task BS10_snapshot_and_player_rows_reject_updates_and_deletes()
    {
        await using var database = new FantasyDbContext(options);
        var (game, players) = await SeedBoxScoreReferences(database);
        await new BoxScoreRepository(database).AddAsync(BoxScore(game, players), TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();
        var snapshot = await database.BoxScoreSnapshots.SingleAsync(TestContext.Current.CancellationToken);
        database.Entry(snapshot).Property(row => row.Confidence).CurrentValue = 0.5m;
        await Should.ThrowAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();
        database.BoxScoreSnapshots.Remove(snapshot);
        await Should.ThrowAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();
        var sample = await database.PlayerGameStats.FirstAsync(TestContext.Current.CancellationToken);
        database.Entry(sample).Property(row => row.Confidence).CurrentValue = 0.5m;
        await Should.ThrowAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();
        database.PlayerGameStats.Remove(sample);
        await Should.ThrowAsync<InvalidOperationException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<(Guid Game, Guid[] Players)> SeedBoxScoreReferences(FantasyDbContext database)
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        var game = Guid.NewGuid();
        var players = new[] { Guid.NewGuid(), Guid.NewGuid() };
        database.NbaTeams.AddRange(NbaTeamRow.Create(home, "Home", "AAA"), NbaTeamRow.Create(away, "Away", "BBB"));
        database.Players.AddRange(players.Select((id, index) => PlayerRow.Create(id, $"Player {index}", $"player {index}", ["C"], null)));
        database.NbaGames.Add(NbaGameRow.Create(game, 2026, new DateTimeOffset(2026, 1, 2, 20, 0, 0, TimeSpan.Zero),
            home, away, 100, 90, "Final", DataSourceName.Manual, game.ToString(),
            new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero), null, "manual-v1", 1m, new string('a', 64)));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (game, players);
    }

    private static CompletedBoxScore BoxScore(Guid game, Guid[] players, int revision = 0,
        decimal points = 0m, string? source = null, NbaGamePhase phase = NbaGamePhase.RegularSeason,
        int? fetchedDay = null, int season = 2026, DateOnly? playedOn = null)
    {
        source ??= DataSourceName.Manual;
        var provenance = new DataProvenance(source, game.ToString(),
            new DateTimeOffset(2026, 1, fetchedDay ?? 3 + revision, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 23, 0, 0, TimeSpan.Zero), $"{source}-v1", 0.95m,
            new string((char)('a' + revision), 64));
        var date = playedOn ?? new DateOnly(2026, 1, 2);
        var samples = players.Select((player, index) => new PlayerGameSample(game, new PlayerId(player), season,
            date, true, index == 0, index == 0 ? new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = points,
                [StatKey.MIN] = 1m + 1m / 60m,
            }) : null, provenance)).ToArray();
        return new CompletedBoxScore(game, season, date, phase, provenance, samples);
    }

    private static Task<IReadOnlyList<PlayerGameSample>> ReadBoxScore(BoxScoreRepository repository) =>
        repository.ListAsync(2026, DataSourceName.Manual, NbaGamePhase.RegularSeason,
            new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken);

    private sealed class ConcurrentBoxScoreSaves : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Both callers have passed the no-existing-snapshot check before either writes.
            if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}
