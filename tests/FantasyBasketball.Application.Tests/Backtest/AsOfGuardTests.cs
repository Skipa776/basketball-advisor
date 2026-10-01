using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Backtest;

public sealed class AsOfGuardTests
{
    private static readonly DateTimeOffset AsOf = new(2025, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task B01_a_box_score_after_as_of_throws_and_names_the_row()
    {
        var gameId = Guid.NewGuid();
        var repository = new AsOfBoxScoreRepository(
            new FakeBoxScoreRepository(
            [
                Game(Guid.NewGuid(), new DateOnly(2025, 8, 1), null),
                Game(gameId, new DateOnly(2025, 11, 20), null),
            ]),
            AsOf);

        var exception = await Should.ThrowAsync<LeakageException>(() => repository.ListAsync(
            2026,
            DataSourceName.Manual,
            NbaGamePhase.RegularSeason,
            new DateOnly(2025, 12, 1),
            TestContext.Current.CancellationToken));

        exception.Message.ShouldContain(gameId.ToString());
    }

    [Fact]
    public async Task B01_a_season_line_is_unknown_until_that_season_ends()
    {
        var playerId = new PlayerId(Guid.NewGuid());
        var line = SeasonLine(playerId, 2025, null);
        var repository = new AsOfSeasonStatLineRepository(
            new FakeSeasonStatLineRepository([line]),
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        await Should.ThrowAsync<LeakageException>(() => repository.ListPoolAsync(
            2025,
            DataSourceName.Manual,
            TestContext.Current.CancellationToken));

        var later = new AsOfSeasonStatLineRepository(
            new FakeSeasonStatLineRepository([line]),
            new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var rows = await later.ListPoolAsync(
            2025,
            DataSourceName.Manual,
            TestContext.Current.CancellationToken);
        rows.Single().ShouldBe(line);
    }

    [Fact]
    public async Task B01_rows_on_or_before_as_of_pass_through_unchanged()
    {
        var seasonLine = SeasonLine(
            new PlayerId(Guid.NewGuid()),
            2025,
            new DateTimeOffset(2025, 6, 20, 0, 0, 0, TimeSpan.Zero));
        var game = Game(Guid.NewGuid(), new DateOnly(2025, 8, 30), null);
        var seasonRepository = new AsOfSeasonStatLineRepository(
            new FakeSeasonStatLineRepository([seasonLine]),
            AsOf);
        var boxRepository = new AsOfBoxScoreRepository(
            new FakeBoxScoreRepository([game]),
            AsOf);
        var token = TestContext.Current.CancellationToken;

        (await seasonRepository.ListPoolAsync(2025, DataSourceName.Manual, token)).SequenceEqual([seasonLine])
            .ShouldBeTrue();
        (await seasonRepository.GetAsync(seasonLine.PlayerId, 2025, DataSourceName.Manual, token))
            .ShouldBe(seasonLine);
        var lateGame = Game(Guid.NewGuid(), new DateOnly(2025, 8, 31), null);
        var lateBoxRepository = new AsOfBoxScoreRepository(
            new FakeBoxScoreRepository([game, lateGame]),
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero));
        (await lateBoxRepository.ListAsync(
            2026,
            DataSourceName.Manual,
            NbaGamePhase.RegularSeason,
            new DateOnly(2025, 9, 1),
            token)).SequenceEqual([game, lateGame]).ShouldBeTrue();
    }

    [Fact]
    public async Task B01_writes_through_the_guard_are_refused()
    {
        var seasonRepository = new AsOfSeasonStatLineRepository(
            new FakeSeasonStatLineRepository([]),
            AsOf);
        var boxRepository = new AsOfBoxScoreRepository(
            new FakeBoxScoreRepository([]),
            AsOf);
        var token = TestContext.Current.CancellationToken;

        var seasonWrite = await Should.ThrowAsync<NotSupportedException>(() => seasonRepository.AddAsync(
            SeasonLine(new PlayerId(Guid.NewGuid()), 2025, null),
            token));
        seasonWrite.Message.ShouldBe("The backtest runner is read-only.");
        var boxWrite = await Should.ThrowAsync<NotSupportedException>(() => boxRepository.AddAsync(
            null!,
            token));
        boxWrite.Message.ShouldBe("The backtest runner is read-only.");
    }

    private static PlayerGameSample Game(Guid gameId, DateOnly playedOn, DateTimeOffset? sourceTimestamp) =>
        new(
            gameId,
            new PlayerId(Guid.NewGuid()),
            2026,
            playedOn,
            true,
            false,
            null,
            Provenance(sourceTimestamp));

    private static SeasonStatLine SeasonLine(
        PlayerId playerId,
        int seasonEndYear,
        DateTimeOffset? sourceTimestamp) =>
        new(
            playerId,
            seasonEndYear,
            50,
            20m,
            new StatLine(new Dictionary<StatKey, decimal>()),
            new StatLine(new Dictionary<StatKey, decimal>()),
            null,
            Provenance(sourceTimestamp));

    private static DataProvenance Provenance(DateTimeOffset? sourceTimestamp) =>
        new(
            DataSourceName.Manual,
            null,
            DateTimeOffset.UnixEpoch,
            sourceTimestamp,
            "manual-v1",
            1m,
            new string('a', 64));

    private sealed class FakeBoxScoreRepository(
        IReadOnlyList<PlayerGameSample> samples) : IBoxScoreRepository
    {
        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BoxScorePool>>([]);

        public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken cancellationToken) =>
            Task.FromResult(samples.Any(sample => sample.GameId == gameId));

        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Fake store is read-only.");

        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(
            int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken) =>
            Task.FromResult(samples);
    }

    private sealed class FakeSeasonStatLineRepository(
        IReadOnlyList<SeasonStatLine> lines) : ISeasonStatLineRepository
    {
        public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SeasonProjectionPool>>([]);

        public Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(
            int seasonEndYear, string source, CancellationToken cancellationToken) =>
            Task.FromResult(lines);

        public Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Fake store is read-only.");

        public Task<SeasonStatLine?> GetAsync(
            PlayerId playerId,
            int seasonEndYear,
            string source,
            CancellationToken cancellationToken) =>
            Task.FromResult<SeasonStatLine?>(lines.FirstOrDefault(line => line.PlayerId == playerId));
    }
}
