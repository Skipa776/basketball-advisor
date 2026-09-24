using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class AccountDataTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();
    private readonly Guid userA = Guid.NewGuid();
    private readonly Guid userB = Guid.NewGuid();
    private readonly Guid userC = Guid.NewGuid();
    private readonly Guid playerId = Guid.NewGuid();
    private readonly Guid baselineId = Guid.NewGuid();
    private DbContextOptions<FantasyDbContext> options = null!;

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var database = Database(userA);
        await database.Database.MigrateAsync(
            TestContext.Current.CancellationToken);
        database.Users.AddRange(
            User(userA, "a"),
            User(userB, "b"),
            User(userC, "c"));
        database.Players.Add(PlayerRow.Create(
            playerId,
            "Portable Shared Player",
            "portable shared player",
            ["G"],
            null));
        database.BaselineProjections.Add(BaselineProjectionRow.Create(
            baselineId,
            playerId,
            30m,
            """{"PTS":0.5}""",
            """{"PTS":15}""",
            70,
            DateTimeOffset.UnixEpoch,
            "account-archive-v1"));
        database.AdjustedProjections.Add(AdjustedProjectionRow.Create(
            Guid.NewGuid(),
            null,
            playerId,
            baselineId,
            """{"PTS":15}""",
            [],
            0m,
            "High",
            1m,
            false,
            DateTimeOffset.UnixEpoch));
        await database.SaveChangesAsync(
            TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task U13_deleting_user_removes_only_their_owned_graph()
    {
        var a = await SeedOwnedGraphAsync(userA, "A");
        var b = await SeedOwnedGraphAsync(userB, "B");
        await using (var database = Database(userA))
        {
            await new AccountDataService(
                    database,
                    new FixedUserContext(userA))
                .DeleteAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = Database(userB);
        (await CountOwnedRowsAsync(
            verification,
            userA,
            TestContext.Current.CancellationToken))
            .ShouldBe(0);
        (await CountOwnedRowsAsync(
            verification,
            userB,
            TestContext.Current.CancellationToken))
            .ShouldBeGreaterThan(0);
        (await verification.FantasyLeagues
            .Select(row => row.Id)
            .SingleAsync(TestContext.Current.CancellationToken))
            .ShouldBe(b.LeagueId);
        (await verification.Players.CountAsync(
            TestContext.Current.CancellationToken))
            .ShouldBe(1);
        (await verification.BaselineProjections.CountAsync(
            TestContext.Current.CancellationToken))
            .ShouldBe(1);
        (await verification.AdjustedProjections.CountAsync(
            TestContext.Current.CancellationToken))
            .ShouldBe(2);
        (await verification.Users.AnyAsync(
            user => user.Id == userA,
            TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        a.LeagueId.ShouldNotBe(b.LeagueId);
    }

    [Fact]
    public async Task U14_export_import_round_trips_the_complete_owned_graph()
    {
        await SeedOwnedGraphAsync(userA, "Portable");
        OwnedDataArchive exported;
        await using (var source = Database(userA))
        {
            exported = await new AccountDataService(
                    source,
                    new FixedUserContext(userA))
                .ExportAsync(TestContext.Current.CancellationToken);
        }

        await using (var destination = Database(userC))
        {
            await new AccountDataService(
                    destination,
                    new FixedUserContext(userC))
                .ImportAsync(
                    exported,
                    TestContext.Current.CancellationToken);
        }

        OwnedDataArchive restored;
        await using (var destination = Database(userC))
        {
            restored = await new AccountDataService(
                    destination,
                    new FixedUserContext(userC))
                .ExportAsync(TestContext.Current.CancellationToken);
        }

        exported.Tables.Count.ShouldBe(14, "league teams, roster entries and eligibility travel with the account");
        PortablePayload(restored).ShouldBe(
            PortablePayload(exported),
            ignoreOrder: true);
    }

    [Fact]
    public async Task Archive_import_rejects_reference_to_another_users_owned_row()
    {
        await SeedOwnedGraphAsync(userA, "Source");
        var other = await SeedOwnedGraphAsync(userB, "Other");
        OwnedDataArchive exported;
        await using (var source = Database(userA))
        {
            exported = await new AccountDataService(
                    source,
                    new FixedUserContext(userA))
                .ExportAsync(TestContext.Current.CancellationToken);
        }

        var tables = exported.Tables.Select(table =>
        {
            if (table.Table != "draft_pick")
            {
                return table;
            }

            var rows = JsonNode.Parse(table.Rows.GetRawText())!.AsArray();
            rows[0]!.AsObject()["draft_session_id"] =
                other.DraftId.ToString();
            return new OwnedTableArchive(
                table.Table,
                JsonDocument.Parse(rows.ToJsonString()).RootElement.Clone());
        }).ToArray();
        var tampered = new OwnedDataArchive(exported.Version, tables);
        await using var destination = Database(userC);
        var service = new AccountDataService(
            destination,
            new FixedUserContext(userC));

        await Should.ThrowAsync<InvalidAccountArchiveException>(() =>
            service.ImportAsync(
                tampered,
                TestContext.Current.CancellationToken));
    }

    private async Task<OwnedGraphIds> SeedOwnedGraphAsync(
        Guid userId,
        string suffix)
    {
        var leagueId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var contextId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        await using var database = Database(userId);
        database.FantasyLeagues.Add(FantasyLeagueRow.Create(
            leagueId,
            $"League {suffix}",
            "Points",
            10,
            [],
            "Daily"));
        database.ScoringRules.Add(ScoringRuleRow.Create(
            leagueId,
            "PTS",
            1m,
            0));
        database.RosterSlots.Add(RosterSlotRow.Create(
            leagueId,
            "UTIL",
            0));
        var teamId = Guid.NewGuid();
        var team = LeagueTeamRow.Create(teamId, leagueId, $"{suffix} team", true, 0);
        team.Entries.Add(LeagueRosterEntryRow.Create(teamId, playerId, 0));
        database.LeagueTeams.Add(team);
        database.LeagueEligibility.Add(LeagueEligibilityRow.Create(leagueId, playerId, ["PG", "SG"]));
        database.DraftSessions.Add(DraftSessionRow.Create(
            draftId,
            leagueId,
            13,
            10,
            1));
        database.DraftPicks.Add(DraftPickRow.Create(
            Guid.NewGuid(),
            draftId,
            playerId,
            1));
        database.ContextEvents.Add(ContextEventRow.Create(
            contextId,
            "RotationChange",
            null,
            playerId,
            [playerId],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            null,
            "Positive",
            0.5m,
            "High",
            null,
            "manual",
            null,
            $"Context {suffix}",
            "Unverified"));
        database.PlayerContextImpacts.Add(PlayerContextImpactRow.Create(
            Guid.NewGuid(),
            contextId,
            playerId,
            3m,
            0.01m,
            0.02m,
            0.03m,
            0.04m,
            0.05m,
            0.06m,
            false));
        database.AdjustedProjections.Add(AdjustedProjectionRow.Create(
            adjustmentId,
            userId,
            playerId,
            baselineId,
            """{"PTS":16.5}""",
            [contextId],
            5m,
            "High",
            0.9m,
            true,
            DateTimeOffset.UnixEpoch.AddMinutes(1)));
        database.FantasyValues.Add(FantasyValueRow.Create(
            Guid.NewGuid(),
            playerId,
            leagueId,
            16.5m,
            1155m,
            adjustmentId));
        database.Recommendations.Add(RecommendationRow.Create(
            recommendationId,
            "Draft",
            playerId,
            90m,
            "High"));
        database.RecommendationEvidence.Add(
            RecommendationEvidenceRow.Create(
                recommendationId,
                "Projection",
                "Positive",
                $"Evidence {suffix}",
                16.5m,
                0));
        await database.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        return new OwnedGraphIds(leagueId, draftId);
    }

    private FantasyDbContext Database(Guid userId) =>
        new(options, new FixedUserContext(userId));

    private static async Task<long> CountOwnedRowsAsync(
        FantasyDbContext database,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            long total = 0;
            var tables = database.Model.GetEntityTypes()
                .Where(entityType =>
                    typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
                .Select(entityType => entityType.GetTableName()!)
                .Distinct(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                await using var command = database.Database
                    .GetDbConnection()
                    .CreateCommand();
                command.CommandText =
                    $"SELECT COUNT(*) FROM \"{table}\" WHERE owner_id = @owner_id;";
                AddParameter(command, "owner_id", userId);
                total += Convert.ToInt64(
                    await command.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            return total;
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static FantasyUser User(Guid id, string suffix) =>
        new()
        {
            Id = id,
            UserName = $"archive-{suffix}",
            NormalizedUserName = $"ARCHIVE-{suffix.ToUpperInvariant()}",
            DisplayName = $"Archive {suffix}",
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private static string[] PortablePayload(OwnedDataArchive archive) =>
        archive.Tables
            .SelectMany(table => table.Rows.EnumerateArray().Select(row =>
            {
                var payload = row.EnumerateObject()
                    .Where(property =>
                        property.Name != "id"
                        && property.Name != "owner_id"
                        && !property.Name.EndsWith("_id", StringComparison.Ordinal)
                        && property.Name != "applied_context_event_ids")
                    .OrderBy(property => property.Name)
                    .ToDictionary(
                        property => property.Name,
                        property => property.Value);
                return $"{table.Table}:{JsonSerializer.Serialize(payload)}";
            }))
            .ToArray();

    private sealed record OwnedGraphIds(Guid LeagueId, Guid DraftId);

    private sealed class FixedUserContext(Guid userId) : IUserContext
    {
        public Guid CurrentUserId => userId;
    }
}
