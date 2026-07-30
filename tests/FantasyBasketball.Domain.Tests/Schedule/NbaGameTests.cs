using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Schedule;

public sealed class NbaGameTests
{
    [Fact]
    public void Game_is_valid_by_construction()
    {
        var home = new NbaTeamId(Guid.NewGuid());
        var away = new NbaTeamId(Guid.NewGuid());
        var startsAt = new DateTimeOffset(2026, 1, 15, 0, 30, 0, TimeSpan.Zero);
        var provenance = new DataProvenance(
            DataSourceName.BallDontLie,
            "9001",
            DateTimeOffset.UnixEpoch,
            startsAt,
            "balldontlie-v1",
            DataSourceConfidence.OfficialApi,
            new string('a', 64));
        var game = new NbaGame(
            Guid.NewGuid(),
            2026,
            startsAt,
            home,
            away,
            112,
            108,
            " Final ",
            provenance);

        game.StartsAt.ShouldBe(startsAt);
        game.HomeTeamId.ShouldBe(home);
        game.AwayTeamId.ShouldBe(away);
        game.Status.ShouldBe("Final");
        game.Provenance.ShouldBe(provenance);
    }

    [Fact]
    public void Game_rejects_invalid_identity_range_and_time()
    {
        var home = new NbaTeamId(Guid.NewGuid());
        var away = new NbaTeamId(Guid.NewGuid());
        var provenance = CreateProvenance();

        Should.Throw<ArgumentException>(() => CreateGame(
            Guid.Empty,
            2026,
            DateTimeOffset.UnixEpoch,
            home,
            away,
            0,
            0,
            "Scheduled",
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateGame(
            Guid.NewGuid(),
            1946,
            DateTimeOffset.UnixEpoch,
            home,
            away,
            0,
            0,
            "Scheduled",
            provenance));
        Should.Throw<ArgumentException>(() => CreateGame(
            Guid.NewGuid(),
            2026,
            DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(1)),
            home,
            away,
            0,
            0,
            "Scheduled",
            provenance));
        Should.Throw<ArgumentException>(() => CreateGame(
            Guid.NewGuid(),
            2026,
            DateTimeOffset.UnixEpoch,
            home,
            home,
            0,
            0,
            "Scheduled",
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateGame(
            Guid.NewGuid(),
            2026,
            DateTimeOffset.UnixEpoch,
            home,
            away,
            -1,
            0,
            "Final",
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateGame(
            Guid.NewGuid(),
            2026,
            DateTimeOffset.UnixEpoch,
            home,
            away,
            0,
            -1,
            "Final",
            provenance));
        Should.Throw<ArgumentException>(() => CreateGame(
            Guid.NewGuid(),
            2026,
            DateTimeOffset.UnixEpoch,
            home,
            away,
            null,
            null,
            "",
            provenance));
    }

    private static NbaGame CreateGame(
        Guid id,
        int seasonEndYear,
        DateTimeOffset startsAt,
        NbaTeamId homeTeamId,
        NbaTeamId awayTeamId,
        int? homeScore,
        int? awayScore,
        string status,
        DataProvenance provenance) =>
        new(
            id,
            seasonEndYear,
            startsAt,
            homeTeamId,
            awayTeamId,
            homeScore,
            awayScore,
            status,
            provenance);

    private static DataProvenance CreateProvenance() =>
        new(
            DataSourceName.BallDontLie,
            "9001",
            DateTimeOffset.UnixEpoch,
            null,
            "balldontlie-v1",
            DataSourceConfidence.OfficialApi,
            new string('a', 64));
}
