using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Players;

public sealed class IdentityDomainTests
{
    public static TheoryData<string, string> NormalizationCases =>
        new()
        {
            { "Nikola Jokić", "nikola jokic" },
            { "De'Aaron Fox", "deaaron fox" },
            { "Karl-Anthony Towns", "karlanthony towns" },
            { "P.J. Tucker", "pj tucker" },
            { "Jaren Jackson Jr.", "jaren jackson" },
            { "  Luka   Dončić ", "luka doncic" },
            { "V Carter", "v carter" },
        };

    [Theory]
    [MemberData(nameof(NormalizationCases))]
    public void Player_name_normalization_is_exact(string input, string expected)
    {
        PlayerName.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Player_name_rejects_an_empty_normalized_value()
    {
        Should.Throw<ArgumentException>(() => PlayerName.Normalize("..."));
    }

    [Fact]
    public void Pending_identity_match_is_valid_by_construction()
    {
        var candidate = new PlayerId(Guid.NewGuid());
        var pending = new PendingIdentityMatch(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            "42",
            "  Player Name  ",
            "player name",
            [candidate, candidate],
            DateTimeOffset.UnixEpoch,
            "Ambiguous");

        pending.FullName.ShouldBe("Player Name");
        pending.CandidatePlayerIds.ShouldBe([candidate]);

        Should.Throw<ArgumentException>(() => new PendingIdentityMatch(
            Guid.Empty,
            DataSourceName.BallDontLie,
            "42",
            "Player",
            "player",
            [candidate],
            DateTimeOffset.UnixEpoch,
            "Ambiguous"));
        Should.Throw<ArgumentException>(() => new PendingIdentityMatch(
            Guid.NewGuid(),
            "unknown",
            "42",
            "Player",
            "player",
            [candidate],
            DateTimeOffset.UnixEpoch,
            "Ambiguous"));
        Should.Throw<ArgumentException>(() => new PendingIdentityMatch(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            "42",
            "Player",
            "player",
            [candidate],
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.FromHours(1)),
            "Ambiguous"));
        Should.Throw<ArgumentException>(() => new PendingIdentityMatch(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            "42",
            "Player",
            "player",
            [],
            DateTimeOffset.UnixEpoch,
            "Ambiguous"));
    }

    [Fact]
    public void Data_import_run_enforces_immutable_lifecycle_snapshots()
    {
        var startedAt = DateTimeOffset.UnixEpoch;
        var finishedAt = startedAt.AddMinutes(1);
        var running = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Running,
            startedAt,
            null,
            0,
            0,
            null);
        var succeeded = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            2,
            1,
            null);
        var failed = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Failed,
            startedAt,
            finishedAt,
            0,
            0,
            "Unavailable");

        running.FinishedAt.ShouldBeNull();
        succeeded.RowsWritten.ShouldBe(2);
        succeeded.PendingIdentityMatches.ShouldBe(1);
        failed.FailureDetail.ShouldBe("Unavailable");
    }

    [Fact]
    public void Data_import_run_rejects_invalid_snapshots()
    {
        var startedAt = DateTimeOffset.UnixEpoch;
        var finishedAt = startedAt.AddMinutes(1);

        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.Empty,
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            "unknown",
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt.ToOffset(TimeSpan.FromHours(1)),
            finishedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt.ToOffset(TimeSpan.FromHours(1)),
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            finishedAt,
            startedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            -1,
            0,
            null));
        Should.Throw<ArgumentOutOfRangeException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            0,
            -1,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Running,
            startedAt,
            finishedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            null,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Failed,
            startedAt,
            finishedAt,
            0,
            0,
            null));
        Should.Throw<ArgumentException>(() => CreateRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Succeeded,
            startedAt,
            finishedAt,
            0,
            0,
            "Unexpected"));
    }

    private static DataImportRun CreateRun(
        Guid id,
        string source,
        DataImportRunStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        int rowsWritten,
        int pendingIdentityMatches,
        string? failureDetail) =>
        new(
            id,
            source,
            status,
            startedAt,
            finishedAt,
            rowsWritten,
            pendingIdentityMatches,
            failureDetail);
}
