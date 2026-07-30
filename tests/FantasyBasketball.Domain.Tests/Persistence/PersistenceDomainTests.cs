using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Persistence;

public sealed class PersistenceDomainTests
{
    [Fact]
    public void Player_is_valid_by_construction()
    {
        Should.Throw<ArgumentException>(() => new Player(
            new PlayerId(Guid.NewGuid()),
            "",
            "name",
            null,
            [],
            null));

        Should.Throw<ArgumentException>(() => new PlayerId(Guid.Empty));
        Should.Throw<ArgumentException>(() => new NbaTeamId(Guid.Empty));
        Should.Throw<ArgumentException>(() => new Player(
            new PlayerId(Guid.NewGuid()),
            "Name",
            "name",
            null,
            ["PG", ""],
            null));

        var teamId = new NbaTeamId(Guid.NewGuid());
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            "  Player Name  ",
            "player name",
            teamId,
            ["PG"],
            new DateOnly(2000, 1, 1));

        player.FullName.ShouldBe("Player Name");
        player.CurrentTeamId.ShouldBe(teamId);
        player.Positions.ShouldBe(["PG"]);

        var team = new NbaTeam(teamId, "Team", "TM");
        team.Abbreviation.ShouldBe("TM");
        Should.Throw<ArgumentException>(() => new NbaTeam(teamId, "", "TM"));
        Should.Throw<ArgumentException>(() => new NbaTeam(teamId, "Team", ""));
        var identity = new ExternalPlayerIdentity(
            player.Id,
            DataSourceName.Manual,
            "42",
            DateTimeOffset.UnixEpoch,
            false);
        identity.ExternalId.ShouldBe("42");
        Should.Throw<ArgumentException>(() => new ExternalPlayerIdentity(
            player.Id,
            "unknown",
            "42",
            DateTimeOffset.UnixEpoch,
            false));
    }

    [Fact]
    public void Provenance_requires_utc_and_bounded_confidence()
    {
        Should.Throw<ArgumentException>(() => CreateProvenance(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.FromHours(1)),
            1m));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateProvenance(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            1.1m));
        Should.Throw<ArgumentException>(() => new DataProvenance(
            DataSourceName.Manual,
            null,
            DateTimeOffset.UnixEpoch,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.FromHours(1)),
            "manual-v1",
            1m,
            new string('c', 64)));
        Should.Throw<ArgumentException>(() => new DataProvenance(
            "unknown",
            null,
            DateTimeOffset.UnixEpoch,
            null,
            "unknown-v1",
            1m,
            new string('c', 64)));
        Should.Throw<ArgumentException>(() => new DataProvenance(
            DataSourceName.Manual,
            null,
            DateTimeOffset.UnixEpoch,
            null,
            "wrong-v1",
            1m,
            new string('c', 64)));
        Should.Throw<ArgumentException>(() => new DataProvenance(
            DataSourceName.Manual,
            null,
            DateTimeOffset.UnixEpoch,
            null,
            "manual-v1",
            1m,
            "not-a-sha-256-hash"));

        var valid = CreateProvenance(DateTimeOffset.UnixEpoch, 0.5m);
        valid.Source.ShouldBe(DataSourceName.Manual);
        valid.Confidence.ShouldBe(0.5m);
    }

    [Fact]
    public void Season_stat_line_rejects_invalid_ranges()
    {
        var playerId = new PlayerId(Guid.NewGuid());
        var line = new StatLine(new Dictionary<StatKey, decimal>());
        var provenance = CreateProvenance(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            1m);

        Should.Throw<ArgumentOutOfRangeException>(() => new SeasonStatLine(
            playerId,
            2026,
            -1,
            0m,
            line,
            line,
            null,
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => new SeasonStatLine(
            playerId,
            2026,
            1,
            1m,
            line,
            line,
            1.1m,
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => new SeasonStatLine(
            playerId,
            1946,
            1,
            1m,
            line,
            line,
            null,
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => new SeasonStatLine(
            playerId,
            2026,
            1,
            -1m,
            line,
            line,
            null,
            provenance));

        var valid = new SeasonStatLine(
            playerId,
            2026,
            1,
            1m,
            line,
            line,
            0.5m,
            provenance);
        valid.PlayerId.ShouldBe(playerId);
        valid.UsageRate.ShouldBe(0.5m);
    }

    private static DataProvenance CreateProvenance(
        DateTimeOffset fetchedAt,
        decimal confidence) =>
        new(
            DataSourceName.Manual,
            null,
            fetchedAt,
            null,
            "manual-v1",
            confidence,
            new string('c', 64));
}
